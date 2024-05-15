// SPDX-License-Identifier: MIT

using System.Collections;
using System.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using fennecs.pools;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace fennecs;

/// <summary>
/// A storage of a class of Entities with a fixed set of Components, its <see cref="Signature"/>.
/// </summary>
public sealed class Archetype : IEnumerable<Entity>
{
    /// <summary>
    /// The TypeExpressions that define this Archetype.
    /// </summary>
    public readonly Signature<TypeExpression> Signature;

    /// <summary>
    /// Get a Span of all Identities contained in this Archetype.
    /// </summary>
    public ReadOnlySpan<Identity> Identities => IdentityStorage.Span;

    internal IStorage[] Storages => _storages;

    /// <summary>
    /// Number of Entities contained in this Archetype.
    /// </summary>
    public int Count => IdentityStorage.Count;

    /// <summary>
    /// Does this Archetype currently contain no Entities?
    /// </summary>
    public bool IsEmpty => Count == 0;
    
    
    /// <summary>
    /// The World this Archetype is a part of.
    /// </summary>
    private readonly World _world;

    /// <summary>
    /// The Entities in this Archetype (filled contiguously from the bottom, as are the storages).
    /// </summary>
    internal readonly Storage<Identity> IdentityStorage;

    /// <summary>
    /// Actual Component data storages. It' is a fixed size array because an Archetype doesn't change.
    /// </summary>
    private readonly IStorage[] _storages;

    private readonly Dictionary<TypeExpression, int> _storageIndices = new();

    /// <summary>
    /// TODO: Buckets for Wildcard Joins (optional optimization for CrossJoin when complex archetypes get hit repeatedly in tight loops).
    /// </summary>
    private readonly ImmutableDictionary<TypeID, IStorage[]> _buckets;

    // Used by Queries to check if the table has been modified while enumerating.
    private int _version;


    internal Archetype(World world, Signature<TypeExpression> signature)
    {
        _world = world;
        _storages = new IStorage[signature.Count];
        
        Signature = signature;
        
        // Build the relation between storages and types, as well as type Wildcards in buckets.
        var finishedTypes = PooledList<TypeID>.Rent();
        var finishedBuckets = PooledList<IStorage[]>.Rent();
        var currentBucket = PooledList<IStorage>.Rent();
        TypeID currentTypeId = 0;

        // Types are sorted by TypeID first, so we can iterate them in order to add them to Wildcard buckets.
        for (var index = 0; index < signature.Count; index++)
        {
            var type = signature[index];
            _storageIndices.Add(type, index);
            _storages[index] = IStorage.Instantiate(type);

            // Time for a new bucket?
            if (currentTypeId != type.TypeId)
            {
                //Finish bucket (exclude null type)
                if (currentTypeId != 0)
                {
                    finishedTypes.Add(currentTypeId);
                    finishedBuckets.Add(currentBucket.ToArray());
                    currentBucket.Dispose();
                    currentBucket = PooledList<IStorage>.Rent();
                }

                currentTypeId = type.TypeId;
            }

            //TODO: Harmless assert, but...  is it pretty? We could disallow TypeExpression 0, or skip null types.
            Debug.Assert(currentTypeId != 0, "Trying to create bucket for a null type.");
            currentBucket.Add(_storages[index]);
        }

        // Get quick lookup for Identity component
        // CAVEAT: This isn't necessarily at index 0 because another
        // TypeExpression may have been created before the first TE of Identity.
        IdentityStorage = GetStorage<Identity>(default);

        // Bake buckets dictionary
        _buckets = Zip(finishedTypes, finishedBuckets);

        currentBucket.Dispose();
        finishedBuckets.Dispose();
        finishedTypes.Dispose();
    }


    private void Match<T>(TypeExpression expression, IList<Storage<T>> result)
    {
        //TODO: Use TypeBuckets as optimization (much faster!).
        foreach (var (type, index) in _storageIndices)
        {
            if (expression.Matches(type))
            {
                result.Add((Storage<T>) _storages[index]);
            }
        }
    }


    internal PooledList<Storage<T>> Match<T>(TypeExpression expression)
    {
        var result = PooledList<Storage<T>>.Rent();
        Match(expression, result);
        return result;
    }


    private static ImmutableDictionary<T, U> Zip<T, U>(IReadOnlyList<T> finishedTypes, IReadOnlyList<U> finishedBuckets) where T : notnull
    {
        var result = finishedTypes
            .Zip(finishedBuckets, (k, v) => new {Key = k, Value = v})
            .ToImmutableDictionary(item => item.Key, item => item.Value);
        return result;
    }


    internal bool Matches(TypeExpression type)
    {
        return type.Matches(Signature);
    }


    internal bool Matches(IReadOnlyList<TypeExpression> types)
    {
        return types.Any(Matches);
    }


    internal bool Matches(Mask mask)
    {
        //Not overrides both Any and Has.
        var matchesNot = !mask.NotTypes.Any(t => t.Matches(Signature));
        if (!matchesNot) return false;

        //If already matching, no need to check any further. 
        var matchesHas = mask.HasTypes.All(t => t.Matches(Signature));
        if (!matchesHas) return false;

        //Short circuit to avoid enumerating all AnyTypes if already matching; or if none present.
        var matchesAny = mask.AnyTypes.Count == 0;
        matchesAny |= mask.AnyTypes.Any(t => t.Matches(Signature));

        return matchesHas && matchesNot && matchesAny;
    }


    internal bool IsMatchSuperSet(IReadOnlyList<TypeExpression> matchTypes)
    {
        var matches = true;
        for (var i = 0; i < matchTypes.Count; i++)
        {
            matches &= matchTypes[i].Matches(Signature);
        }

        return matches;
    }


/*    [Obsolete("Don't add identities 1 by 1", true)]
    internal int Add(Identity identity)
    {
        Interlocked.Increment(ref _version);

        /*
        EnsureCapacity(Count + 1);
        _identities[Count] = identity;
        return Count++;
    }
*/

    internal void Remove(int entry)
    {
        Interlocked.Increment(ref _version);

        foreach (var storage in _storages)
        {
            storage.Delete(entry);
        }
    }


    /// <summary>
    ///  Remove Entities from the Archetype that exceed a given count.
    /// </summary>
    /// <param name="maxEntityCount"></param>
    public void Truncate(int maxEntityCount)
    {
        var excess = Math.Clamp(Count - maxEntityCount, 0, Count);
        if (excess <= 0) return;

        // TODO: Build bulk deletion?
        // IDEA: Just return a chunk from IdentityStorage back to the pool?
        var toDelete = Identities.Slice(Count - excess, excess);
        for (var i = toDelete.Length - 1; i >= 0; i--)
        {
            _world.Despawn(new Entity(_world, toDelete[i]));
        }
    }


    /// <summary>
    /// Moves all Entities from this Archetype to the destination Archetype back-filling with the provided Components.
    /// </summary>
    /// <param name="destination">the Archetype to move the entities to</param>
    /// <param name="additions">the new components and their TypeExpressions to add to the destination Archetype</param>
    /// <param name="backFills">values for each addition to add</param>
    internal void Migrate(Archetype destination, PooledList<TypeExpression> additions, PooledList<object> backFills)
    {
        if (destination == this)
        {
            destination.Fill(additions, backFills, 0, Count);
            return;
        }

        // Mark identities as moved
        for (var i = 0; i < Count; i++)
        {
            _world.GetEntityMeta(IdentityStorage[i]).Archetype = destination;
        }
        
        // Subtractive copy
        foreach (var type in Signature)
        {
            if (!destination.Signature.Contains(type)) continue;
            var srcStorage = GetStorage(type);
            var destStorage = destination.GetStorage(type);
            srcStorage.Migrate(destStorage);
        }

        // Additive back-fill of values
        //FIXME: TODO: How does this work with the new storages?
        //destination.Append(additions, backFills, destination.Count, Count);

        Interlocked.Increment(ref _version);
    }


    /// <summary>
    /// Fills all matching Storages of the archetype with each of the provided values.
    /// </summary>
    /// <param name="types">typeExpressions which storages to fill</param>
    /// <param name="values">values for the types</param>
    /// <param name="start">the index to start filling from</param>
    /// <param name="count">how many elements to fill</param>
    internal void Fill(PooledList<TypeExpression> types, PooledList<object> values, int start, int count)
    {
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            var value = values[i];
            var storage = GetStorage(type);
            
            //FIXME: Split this up in Append and Blit
            storage.Blit(value);
        }
    }

    /// <summary>
    /// Fills the appropriate storage of the archetype with the provided value.
    /// </summary>
    internal void Fill<T>(TypeExpression type, T value)
    {
        var storage = GetStorage<T>(type.Target);
        storage.Blit(value);
    }


    internal Storage<T> GetStorage<T>(Identity target)
    {
        var type = TypeExpression.Of<T>(target);
        return (Storage<T>) GetStorage(type);
    }


    internal IStorage GetStorage(TypeExpression typeExpression) => _storages[_storageIndices[typeExpression]];

    
    internal void Set<T>(TypeExpression typeExpression, T value, int newRow)
    {
        // DeferredOperation sends data as objects
        if (typeof(T).IsAssignableFrom(typeof(object)))
        {
            var sysArray = GetStorage(typeExpression);
            //TODO: Settle on whether storing null values is desirable
            sysArray.Store(newRow, value!);
            return;
        }

        var storage = (Storage<T>) GetStorage(typeExpression);
        storage.Store(newRow, value);
    }

    internal void BackFill<T>(TypeExpression typeExpression, T value)
    {
        // DeferredOperation sends data as objects (decorated with TypeExpressions)
        if (typeof(T).IsAssignableFrom(typeof(object)))
        {
            var sysArray = GetStorage(typeExpression);
            //TODO: Settle on whether storing null values is desirable
            sysArray.Append(value!);
            return;
        }

        var storage = (Storage<T>) GetStorage(typeExpression);
        storage.Append(value);
    }


    internal static int MoveEntry(int entry, Archetype source, Archetype destination)
    {
        // We do this at the start to flag down any running, possibly async enumerators.
        Interlocked.Increment(ref source._version);
        Interlocked.Increment(ref destination._version);

        // Mark entity as moved in Meta.
        var identity = source.Identities[entry];
        source._world.GetEntityMeta(identity).Archetype = destination;
        
        if (destination._storageIndices.Keys.Any(k => !source._storageIndices.ContainsKey(k)))
        {
            //throw new InvalidOperationException("Destination Archetype has more types than source Archetype, a back-fill value would be needed.");
        }
        
        foreach (var (type, oldIndex) in source._storageIndices)
        {
            if (!destination._storageIndices.TryGetValue(type, out var newIndex))
            {
                // Move is subtractive, discard anything we don't have in the destination
                source._storages[oldIndex].Delete(entry);
                continue;
            }

            var oldStorage = source._storages[oldIndex];
            var newStorage = destination._storages[newIndex];

            oldStorage.Move(entry, newStorage);
        }

        return destination.Count-1;
    }


    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder("Archetype ");
        sb.AppendJoin("\n", Signature);
        return sb.ToString();
    }


    /// <inheritdoc />
    public IEnumerator<Entity> GetEnumerator()
    {
        var snapshot = Volatile.Read(ref _version);
        for (var i = 0; i < Count; i++)
        {
            if (snapshot != Volatile.Read(ref _version)) throw new InvalidOperationException("Collection modified while enumerating.");
            yield return new Entity(_world, IdentityStorage[i]);
        }
    }


    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Returns (constructs) the Entity at the given index, associated with the World this Archetype belongs to.
    /// </summary>
    /// <remarks>
    /// There's no bounds checking, so be sure to check against the Count property before using this method.
    /// (This is a performance optimization to avoid the overhead of bounds checking and exceptions in tight loops.)
    /// </remarks>
    public Entity this[int index] => new(_world, IdentityStorage[index]);


    #region Cross Joins
    internal Match.Join<C0> CrossJoin<C0>(TypeExpression[] streamTypes)
    {
        return IsEmpty ? default : new Match.Join<C0>(this, streamTypes);
    }


    internal Match.Join<C0, C1> CrossJoin<C0, C1>(TypeExpression[] streamTypes)
    {
        return IsEmpty ? default : new Match.Join<C0, C1>(this, streamTypes);
    }


    internal Match.Join<C0, C1, C2> CrossJoin<C0, C1, C2>(TypeExpression[] streamTypes)
    {
        return IsEmpty ? default : new Match.Join<C0, C1, C2>(this, streamTypes);
    }


    internal Match.Join<C0, C1, C2, C3> CrossJoin<C0, C1, C2, C3>(TypeExpression[] streamTypes)
    {
        return IsEmpty ? default : new Match.Join<C0, C1, C2, C3>(this, streamTypes);
    }


    internal Match.Join<C0, C1, C2, C3, C4> CrossJoin<C0, C1, C2, C3, C4>(TypeExpression[] streamTypes)
    {
        return IsEmpty ? default : new Match.Join<C0, C1, C2, C3, C4>(this, streamTypes);
    }
    #endregion

    public void Spawn(int count, object[] components)
    {
        using var worldLock = _world.Lock;
        
        foreach (var component in components)
        {
            var type = TypeExpression.Of(component.GetType());
            var storage = GetStorage(type);
            //Array.Fill(storage, component, Count, count);
        }
    }
}