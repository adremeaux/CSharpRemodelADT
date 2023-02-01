# CSharpRemodelADT

Code generator for creating C# Algebraic Data Type implementations from a template

Based on Facebook's Remodel tool: https://github.com/facebook/remodel

#### Build

Compile for .NET 7+ (https://github.com/dotnet/roslyn):  
`csc remodel.cs`

#### Run

`./remodel <dir>`

When run with `./remodel <dir>`, will recursively search for all .adtValue files in the
directories and generate corresponding <class_name>.cs files in the same directory

#### adtValue File Format

```
#Commented Line. No trailing comments allowed.
<Set arguments>
<classname> {
  <baseLevelVarType> <name>
  %<baseLevelVarWithDefaultType> <name>
  #<fixedLineInsertion>
  <fieldname1> {
    <fieldVarType> <name>
  }
  <fieldname2> {
    <fieldVarType> <name>
  }
}
```

#### Modifiers:

- Preceding a base level variable with `%` will generate it with a default value in the constructor:

```
//SomeField.adtValue
//int i
//%SomeClass sc

public sealed class SomeField {
  public SomeField(int i, SomeClass sc = default(SomeClass)) {
  ...
```

- Preceding a base level line with a `#` will cause that lined to be inserted into the base class
  as-is with no modification. You can supply numerous lines. You usually shouldn't use this as it breaks the
  standard for the pattern.

- `Set` Arguments:
  At the start of the file, type `Set <some_arg>` to enable specific features:

  - `Set AllowNulls`
    This will create null defaults in the match functions so you don't have to match the whole thing
  - `Set KnownTypes`
    This will generate `[KnownType...]` directives at the top for json serialization with Newtonsoft
