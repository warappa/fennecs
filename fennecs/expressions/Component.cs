﻿namespace fennecs;

/// <summary>
/// A specific set of match expressions to match specific component types where type parameters are not available.
/// </summary>
public readonly record struct Component
{
    private Component(TypeExpression value)
    {
        this.value = value;
    }
    
    internal TypeExpression value { get; }

    internal bool Matches(Component other) => value.Matches(other.value);

    /// <summary>
    /// Strongly-Typed Wildcard for a specific component type, with or without a Target. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component AnyAny<T>() => new(TypeExpression.Of<T>(new(new(-1, 0))));
    /// <summary>
    /// Strongly-Typed Wildcard for a specific component type, with any (but not no) Target. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component AnyRelation<T>() => new(TypeExpression.Of<T>(new(new(-2, 0))));
    /// <summary>
    /// Wildcard for a specific component type, with any Entity-Entity Relation. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component AnyEntity<T>() => new(TypeExpression.Of<T>(new(new(-3, 0))));
    /// <summary>
    /// Strongly-Typed for a specific component type, with any Object Link. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component AnyObject<T>() => new(TypeExpression.Of<T>(new(new(-4, 0))));
    /// <summary>
    /// Strongly-Typed for a specific component type, with no Relation. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component PlainComponent<T>() => new(TypeExpression.Of<T>(new(default)));
    /// <summary>
    /// Strongly-Typed for a specific component type, with a specific Entity-Entity Relation. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component SpecificEntity<T>(Entity target) => new(TypeExpression.Of<T>(target));
    /// <summary>
    /// Strongly-Typed for a specific component type, with a specific Object Link Relation. Used for Stream Filtering and CRUD.
    /// </summary>
    public static Component SpecificLink<T>(T target) where T : class => new(TypeExpression.Of<T>(fennecs.Link.With(target)));
}


public interface IBlittable
{
    internal TypeExpression TypeExpression { get; }
}

public interface IComponent
{
    internal TypeExpression TypeExpression { get; }
}

/// <summary>
/// 
/// </summary>
/// <param name="match"></param>
/// <typeparam name="T"></typeparam>
public readonly record struct Blittable<T>(Match match = default) : IBlittable, IComponent where T : unmanaged
{
    TypeExpression IBlittable.TypeExpression => TypeExpression.Of<T>(match);
    TypeExpression IComponent.TypeExpression => TypeExpression.Of<T>(match);

    /// <summary>
    /// 
    /// </summary>
    public Match match { get; } = match;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="match0"></param>
    /// <returns></returns>
    public static Blittable<T> Matching(Match match0) => new(match0);


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static Blittable<T> Plain => new(default);

}
