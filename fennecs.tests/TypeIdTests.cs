// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace fennecs.tests;

public class TypeIdTests
{
    [Fact]
    public void TypeId_is_64_bits()
    {
        Assert.Equal(64 / 8, Marshal.SizeOf<TypeExpression>());
    }


    [Fact]
    public void Identity_is_64_bits()
    {
        Assert.Equal(64 / 8, Marshal.SizeOf<Identity>());
    }


    [Fact]
    public void TypeAssigner_Id_Unique()
    {
        Assert.NotEqual(
            LanguageType<int>.Id,
            LanguageType<string>.Id);

        Assert.NotEqual(
            LanguageType<ushort>.Id,
            LanguageType<short>.Id);

        Assert.NotEqual(
            LanguageType<Type1>.Id,
            LanguageType<Type2>.Id);
    }


    [Fact]
    public void TypeAssigner_Id_Same_For_Same_Type()
    {
        Assert.Equal(
            LanguageType<int>.Id,
            LanguageType<int>.Id);

        Assert.Equal(
            LanguageType<Type1>.Id,
            LanguageType<Type1>.Id);

        Assert.Equal(
            LanguageType<Type2>.Id,
            LanguageType<Type2>.Id);

        Assert.Equal(
            LanguageType<Dictionary<string, string>>.Id,
            LanguageType<Dictionary<string, string>>.Id);
    }


    [Fact]
    public void TypeAssigner_None_Matches_Identical()
    {
        var id1 = TypeExpression.Of<int>();
        var id2 = TypeExpression.Of<int>();

        Assert.True(id1.Matches(id2));
    }


    [Fact]
    public void TypeAssigner_Any_Matches_Default()
    {
        // Keeping the default case to ensure it remains at default
        // ReSharper disable once RedundantArgumentDefaultValue
        var id1 = TypeExpression.Of<int>(Match.Plain);
        var id2 = TypeExpression.Of<int>(Match.Any);

        Assert.True(id2.Matches(id1));
        Assert.False(id1.Matches(id2)); //Non-commutative, TODO: Review whether we actually want this.
    }


    [Fact]
    public void TypeAssigner_does_not_Match_Identical()
    {
        var id1 = TypeExpression.Of<int>();
        var id2 = TypeExpression.Of<float>();

        Assert.False(id1.Matches(id2));
    }


    [Fact]
    public void TypeAssigner_None_does_not_match_Any()
    {
        var id1 = TypeExpression.Of<int>();
        var id2 = TypeExpression.Of<int>(new(new Identity(123)));
        var id3 = TypeExpression.Of<int>(Match.Any);

        Assert.False(id1.Matches(id2));
        Assert.False(id1.Matches(id3));
    }


    [Fact]
    public void TypeId_from_Generic_is_same_as_Identify()
    {
        var id1 = TypeExpression.Of<int>().TypeId;
        var id2 = LanguageType.Identify(typeof(int));
        Assert.Equal(id1, id2);

        _ = TypeExpression.Of<string>();

        var id3 = LanguageType.Identify(typeof(bool));
        var id4 = TypeExpression.Of<bool>().TypeId;
        Assert.Equal(id3, id4);

        Assert.NotEqual(id1, id3);
        Assert.NotEqual(id2, id4);
    }


    [Fact]
    public void Match_from_Anonymous_Object_Identity_Is_Same_As_Link()
    {
        object target = new { };
        var typeExpression1 = TypeExpression.Of<object>(Link.With(target));
        var typeExpression2 = TypeExpression.Of<object>(Link.With(target));
        
        Assert.Equal(typeExpression1, typeExpression2);
    }
    private struct Type1;

    private struct Type2;
}