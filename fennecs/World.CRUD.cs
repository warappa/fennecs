namespace fennecs;

public partial class World
{
    #region CRUD
    internal void AddComponent<T>(Entity entity, TypeExpression typeExpression, T data) where T : notnull
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (Mode == WorldMode.Deferred)
        {
            _deferredOperations.Enqueue(new DeferredOperation {Opcode = Opcode.Add, Identity = entity, TypeExpression = typeExpression, Data = data});
            return;
        }

        ref var meta = ref _meta[entity.Index];
        var oldArchetype = meta.Archetype;

        if (oldArchetype.Signature.Matches(typeExpression)) throw new ArgumentException($"Entity {entity} already has a component of type {typeExpression}");

        var newSignature = oldArchetype.Signature.Add(typeExpression);
        var newArchetype = GetArchetype(newSignature);
        Archetype.MoveEntry(meta.Row, oldArchetype, newArchetype);

        // Back-fill the new value
        newArchetype.BackFill(typeExpression, data, 1);
    }

    
    internal void RemoveComponent(Entity entity, TypeExpression typeExpression)
    {
        if (Mode == WorldMode.Deferred)
        {
            _deferredOperations.Enqueue(new DeferredOperation {Opcode = Opcode.Remove, Identity = entity, TypeExpression = typeExpression});
            return;
        }

        ref var meta = ref _meta[entity.Index];

        var oldArchetype = meta.Archetype;

        if (!oldArchetype.Signature.Matches(typeExpression)) throw new ArgumentException($"Entity {entity} does not have a component of type {typeExpression}");

        var newSignature = oldArchetype.Signature.Remove(typeExpression);
        var newArchetype = GetArchetype(newSignature);
        Archetype.MoveEntry(meta.Row, oldArchetype, newArchetype);
    }


    internal bool HasComponent<T>(Entity entity, Match match)
    {
        var type = TypeExpression.Of<T>(match);
        return HasComponent(entity, type);
    }

    /* This is sad but can't be done syntactically at the moment (without bloating the interface)
    internal ref T GetOrCreateComponent<T>(entity entity, Match match) where T : notnull, new()
    {
        AssertAlive(entity);

        if (!HasComponent<T>(entity, match))
        {
            if (Mode != WorldMode.Immediate) throw new InvalidOperationException("Cannot create bew mutable reference to component in deferred mode. (the Entity did must already have the component)");
            AddComponent<T>(entity, TypeExpression.Of<T>(match), new());
        }

        var (table, row, _) = _meta[entity.Index];
        var storage = table.GetStorage<T>(match);
        return ref storage.Span[row];
    }
    */
    
    internal ref T GetComponent<T>(Entity entity, Match match)
    {
        if (!HasComponent<T>(entity, match))
        {
            throw new InvalidOperationException($"Entity {entity} does not have a reference type component of type {typeof(T)} / {match}");
        }

        var (table, row, _) = _meta[entity.Index];
        var storage = table.GetStorage<T>(match);
        return ref storage.Span[row];
    }
    
/*
    internal T GetComponent<T>(entity entity, Match match) where T : class
    {
        AssertAlive(entity);

        if (!HasComponent<T>(entity, match))
        {
           throw new InvalidOperationException($"Entity {entity} does not have a reference type component of type {typeof(T)}");
        }

        var (table, row, _) = _meta[entity.Index];
        var storage = table.GetStorage<T>(match);
        return storage.Span[row];
    }
*/

    internal Signature GetSignature(Entity entity)
    {
        var meta = _meta[entity.Index];
        var array = meta.Archetype.Signature;
        return array;
    }
    #endregion

    internal T[] Get<T>(Entity id, Match match)
    {
        var type = TypeExpression.Of<T>(match);
        var meta = _meta[id.Index];
        using var storages = meta.Archetype.Match<T>(type);
        return storages.Select(s => s[meta.Row]).ToArray();
    }
}