using System.Runtime.InteropServices;

namespace fennecs;

[StructLayout(LayoutKind.Explicit)]
internal record struct Id : IEquatable<object>
{
    [FieldOffset(0)]
    public ulong value;

    [FieldOffset(0)]
    internal int index; // Index in World Meta, or HashCode for Object

    [FieldOffset(4)]
    private ushort decoration; //Generation or TypeID for Object

    [FieldOffset(6)]
    private short header; //World index or Global Virtual Entity Class

    //TODO Remove us when old classes retired. :)
    public static implicit operator Identity(Id self) => self.Identity;
    public static implicit operator Entity(Id self) => new(self.World, self.Identity);

    public Id(IdClass idClass, TypeID type, int value)
    {
        header = (short) idClass;
        decoration = (ushort) type;
        index = value;
        this.value = (ulong) value << 32 | (ulong) type << 16 | (ushort) idClass;
    }
    
    internal Identity Identity => new(value);

    private World World => World.All[header];
    
    internal ref Meta Meta => ref World.GetEntityMeta(this);
    
    internal Id Successor => this with { decoration = (ushort)(decoration + 1) };
    internal Type Type => header switch
    {
        // Decoration is Object Type Id
        -1 => LanguageType.Resolve((TypeID) decoration),

        // Decoration is Generation
        _ => typeof(Identity),
    };

    internal IdClass Class => header switch
    {
        > 0 => IdClass.Entity,
        _ => (IdClass) header,
    };

    bool IEquatable<object>.Equals(object? obj) => 
        (obj is Id other && other.value == value) || 
        (Class == IdClass.Object && obj != null && obj.GetHashCode() == index);

    public override int GetHashCode() => value.GetHashCode();
    
    public static Id Of<T>(T target) where T : class => new(IdClass.Object, LanguageType<T>.Id, target.GetHashCode());

    #region CRUD

    /// <summary>
    /// Gets a reference to the Component of type <typeparamref name="C"/> for the entity.
    /// </summary>
    /// <remarks>
    /// Adds the component before if possible.
    /// </remarks>
    /// <param name="match">specific (targeted) Match Expression for the component type. No wildcards!</param>
    /// <typeparam name="C">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the Entity is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for entity.</exception>
    public ref C Ref<C>(Match match) where C : struct => ref World.GetComponent<C>(this, match);


    /// <inheritdoc cref="Ref{C}(fennecs.Match)"/>
    public ref C Ref<C>() => ref World.GetComponent<C>(this, Match.Plain);

    
    
    /// <summary>
    /// Gets a reference to the Object Link Target of type <typeparamref name="L"/> for the entity.
    /// </summary>
    /// <param name="link">object link match expressioon</param>
    /// <typeparam name="L">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the Entity is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for entity.</exception>
    public ref L Ref<L>(Link<L> link) where L : class => ref World.GetComponent<L>(this, link);


    /// <inheritdoc />
    public Entity Add<T>(Entity relation) where T : notnull, new() => Add(new T(), relation);

    
    /// <inheritdoc cref="Add{R}(R,fennecs.Entity)"/>
    public Entity Add<R>(R value, Entity relation) where R : notnull
    {
        World.AddComponent(Identity, TypeExpression.Of<R>(relation), value);
        return this;
    }

    /// <summary>
    /// Adds a object link to the current entity.
    /// Object links, in addition to making the object available as a Component,
    /// place all Entities with a link to the same object into a single Archetype,
    /// which can optimize processing them in queries.
    /// </summary>
    /// <remarks>
    /// Beware of Archetype fragmentation! 
    /// You can end up with a large number of Archetypes with few Entities in them,
    /// which negatively impacts processing speed and memory usage.
    /// Try to keep the size of your Archetypes as large as possible for maximum performance.
    /// </remarks>
    /// <typeparam name="T">Any reference type. The type the object to be linked with the entity.</typeparam>
    /// <param name="link">The target of the link.</param>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(Link<T> link) where T : class
    {
        World.AddComponent(Identity, TypeExpression.Of<T>(link), link.Target);
        return this;
    }

    /// <inheritdoc />
    public Entity Add<C>() where C : notnull, new() => Add(new C());

    /// <summary>
    /// Adds a Plain Component of a specific type, with specific data, to the current entity. 
    /// </summary>
    /// <param name="data">The data associated with the relation.</param>
    /// <typeparam name="T">Any value or reference component type.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(T data) where T : notnull => Add(data, default);
    

    /// <summary>
    /// Removes a Component of a specific type from the current entity.
    /// </summary>
    /// <typeparam name="C">The type of the Component to be removed.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<C>() where C : notnull
    {
        World.RemoveComponent(Identity, TypeExpression.Of<C>(Match.Plain));
        return this;
    }

    
    /// <summary>
    /// Removes a relation of a specific type between the current entity and the target entity.
    /// </summary>
    /// <param name="relation">target of the relation.</param>
    /// <typeparam name="R">backing type of the relation to be removed.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<R>(Entity relation) where R : notnull
    {
        World.RemoveComponent(Identity, TypeExpression.Of<R>(new Relate(relation)));
        return this;
    }
    
    /// <inheritdoc />
    public Entity Remove<L>(L linkedObject) where L : class => Remove(Link<L>.With(linkedObject));


    /// <summary>
    /// Removes the link of a specific type with the target object.
    /// </summary>
    /// <typeparam name="T">The type of the link to be removed.</typeparam>
    /// <param name="link">The target object from which the link will be removed.</param>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<T>(Link<T> link) where T : class
    {
        World.RemoveComponent(Identity, link.TypeExpression);
        return this;
    }


    /// <summary>
    /// Despawns the Entity from the World.
    /// </summary>
    /// <remarks>
    /// The entity builder struct still exists afterwards, but the entity is no longer alive and subsequent CRUD operations will throw.
    /// </remarks>
    public void Despawn() => World.Despawn(this);


    /// <summary>
    /// Checks if the Entity has a Plain Component.
    /// Same as calling <see cref="Has{T}()"/> with <see cref="Identity.Plain"/>
    /// </summary>
    public bool Has<T>() where T : notnull => World.HasComponent<T>(Identity, default);

    
    /// <inheritdoc />
    public bool Has<R>(Entity relation) where R : notnull => World.HasComponent<R>(Identity, new Relate(relation));

    
    /// <inheritdoc />
    public bool Has<L>(L linkedObject) where L : class => Has(Link<L>.With(linkedObject));


    /// <summary>
    /// Checks if the Entity has a Component of a specific type.
    /// Allows for a <see cref="Match"/> Expression to be specified (Wildcards)
    /// </summary>
    public bool Has<T>(Match match) => World.HasComponent<T>(Identity, match);

    /// <summary>
    /// Checks if the Entity has an Object Link of a specific type and specific target.
    /// </summary>
    public bool Has<T>(Link<T> link) where T : class => World.HasComponent<T>(Identity, link);

    /// <summary>
    /// Boxes all the Components on the entity into an array.
    /// Use sparingly, but don't be paranoid. Suggested uses: serialization and debugging.
    /// </summary>
    /// <remarks>
    /// Values and References are copied, changes to the array will not affect the Entity.
    /// Changes to objects in the array will affect these objects in the World.
    /// This array is re-created every time this getter is called.
    /// The values are re-boxed each time this getter is called.
    /// </remarks>
    public IReadOnlyList<Component> Components => World.GetComponents(Identity);
    
    
    /// <summary>
    /// Gets all Components of a specific type and match expression on the Entity.
    /// Supports relation Wildcards, for example:<ul>
    /// <li><see cref="Entity.Any">Entity.Any</see></li>
    /// <li><see cref="Link.Any">Link.Any</see></li>
    /// <li><see cref="Match.Target">Match.Target</see></li>
    /// <li><see cref="Match.Any">Match.Any</see></li>
    /// <li><see cref="Match.Plain">Match.Plain</see></li>
    /// </ul>
    /// </summary>
    /// <remarks>
    /// This is not intended as the main way to get a component from an entity. Consider <see cref="Stream"/>s instead.
    /// </remarks>
    /// <param name="match">match expression, supports wildcards</param>
    /// <typeparam name="T">backing type of the component</typeparam>
    /// <returns>array with all the component values stored for this entity</returns>
    public T[] Get<T>(Match match) => World.Get<T>(Identity, match);  
    
    #endregion

}

internal enum IdClass : short
{
    Entity = 1,
    None = default,
    Object = -1,
    WildAny = -100,
    WildObject = -200,
    WildEntity = -300,
    WildTarget = -400,
}

record struct Txpr
{
    public TypeID type;
    public Id target;
}