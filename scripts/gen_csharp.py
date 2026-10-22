import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1] / "curriculum" / "csharp"
ROOT.mkdir(parents=True, exist_ok=True)

def q(prompt, options, correct, explanation, option_explanations):
    return {
        "prompt": prompt,
        "options": options,
        "correctIndex": correct,
        "explanation": explanation,
        "optionExplanations": option_explanations,
    }

def lesson(**kw):
    base = {
        "skill": "csharp",
        "recallQuestions": [],
    }
    base.update(kw)
    return base

lessons = []

lessons.append(lesson(
    id="csharp-01-dotnet-csharp-runtime",
    topic="fundamentals",
    order=1,
    title="C#, .NET, and what happens when you run code",
    summary="Separate the language from the platform, then follow a program from source to running process.",
    estimatedMinutes=30,
    theory=r"""
C# and .NET are related, but they are not the same thing.

**C# is a language.** It defines syntax and semantics: types, classes, methods, `foreach`, `async`, and so on. A C# file is just text until a compiler reads it.

**.NET is a platform.** It gives you a runtime, a standard library, and a toolchain. The important pieces:

- **SDK** — the tools: `dotnet build`, `dotnet run`, templates, the C# compiler.
- **Runtime** — what executes your compiled program. It includes the **CLR** (Common Language Runtime).
- **Base Class Library (BCL)** — `System.*` types such as `Console`, `List<T>`, `HttpClient`, `Task`.

C# compiles to **Intermediate Language (IL)**, not directly to native machine code. At run time the CLR loads the IL and the JIT (just-in-time) compiler turns the methods you actually call into machine code for the current CPU and operating system. That is why the same `.dll` can run on Windows, Linux, or macOS when a compatible .NET runtime is installed.

A minimal project has a project file and some code:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

```csharp
// Program.cs — top-level statements become the entry point
Console.WriteLine("Learning Hub");
```

`ImplicitUsings` is why `Console` is visible without a `using System;` line. Under the hood the compiler still resolves it to `System.Console`. A `using` directive only imports names; it does not load a library at run time.

When you type `dotnet run`, the CLI restores NuGet dependencies, compiles the project to IL, creates an executable host, starts the runtime, and invokes the generated entry point. `dotnet build` stops before running. `dotnet publish` prepares files for deployment, optionally with trimming or ahead-of-time compilation.

The practical mental model: **the compiler checks the shape of your program; the runtime supplies objects, memory, threads, and services; the JIT makes it fast on this machine.**
""",
    workedExample={
        "title": "Trace `dotnet run` in a console app",
        "markdown": r"""
Suppose the project contains only the `Program.cs` shown above. Trace what happens, in order, when you run `dotnet run`.
""",
        "steps": [
            "The CLI reads the `.csproj`, discovers the target framework (`net10.0`) and that this is an executable project.",
            "NuGet restore resolves package references. A project with no packages still has the framework reference to the shared .NET runtime.",
            "The C# compiler parses `Program.cs`, builds a syntax tree, checks types, and emits IL into a `.dll`. Top-level statements are rewritten into a synthesized `Program.Main` method.",
            "The host starts the CLR. The CLR finds `Main`, loads referenced assemblies, and the JIT compiles `Main` to machine code for this CPU.",
            "`Console.WriteLine` executes. `Console` lives in the BCL and eventually writes to the process's standard output — which your terminal is showing.",
            "When `Main` returns, the process exits. Memory owned by the runtime is reclaimed by the operating system, even if the garbage collector never runs."
        ]
    },
    explainPrompt="Explain the difference between the C# compiler, the CLR, and the JIT. Where does each one sit in the journey from a `.cs` file to output on screen?",
    checkQuestions=[
        q(
            "What does the C# compiler produce when it compiles a normal .NET project?",
            ["Native machine code for the current CPU", "Intermediate Language (IL) in an assembly", "A JavaScript bundle", "A running process"],
            1,
            "The compiler emits IL into an assembly (a .dll or .exe). Native code is produced later, by the JIT, on the machine that runs it.",
            ["C# does not normally compile straight to machine code; that is the JIT's job.", "Correct: assemblies contain IL plus metadata.", "That describes a web front-end toolchain, not .NET.", "Compiling and running are separate steps."],
        ),
        q(
            "Which statement best describes the CLR?",
            ["It is the C# language specification.", "It is the runtime that loads assemblies, manages memory, and executes IL.", "It is a NuGet package manager.", "It is the compiler's parser."],
            1,
            "The CLR is the execution engine: loading, memory management, type safety, threads, and JIT compilation all live there.",
            ["The specification describes the language; the CLR executes compiled code.", "Correct.", "NuGet is a package manager and is not the runtime.", "Parsing is a compiler step, not the runtime."],
        ),
        q(
            "Why can the same compiled .NET assembly run on Windows and Linux?",
            ["Because IL is portable and each platform has a .NET runtime that JIT-compiles it", "Because the compiler emits both Windows and Linux binaries", "Because C# is interpreted line by line", "Because Windows emulates Linux in .NET"],
            0,
            "The assembly contains portable IL. A platform-specific runtime and JIT adapt it to the local OS and CPU.",
            ["Correct: portable IL plus a local runtime.", "The compiler does not emit one binary per OS by default.", "C# is not interpreted line by line; it is compiled to IL.", "No emulation is required; .NET has native runtime builds per platform."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-02-values-and-references",
    topic="fundamentals",
    order=2,
    title="Values, references, and what assignment copies",
    summary="Build the mental model that prevents a whole class of aliasing and mutation bugs.",
    estimatedMinutes=35,
    theory=r"""
Every C# type is either a **value type** or a **reference type**. The distinction is about what a variable contains and what assignment copies.

A **value type** variable contains the value itself. `int`, `double`, `bool`, `char`, `DateTime`, and every `struct` are value types. Assigning one to another copies the bits:

```csharp
struct Point { public int X; }
var a = new Point { X = 1 };
var b = a;      // copy
b.X = 9;
Console.WriteLine(a.X); // 1
```

A **reference type** variable contains a reference to an object. Classes, arrays, delegates, and records declared as classes are reference types. Assignment copies the **reference**, not the object:

```csharp
var first = new List<int> { 1, 2 };
var second = first;   // same list
second.Add(3);
Console.WriteLine(first.Count); // 3
```

`string` is a reference type but behaves unusually because it is **immutable**. Operations such as concatenation create a new string; they never edit the old one:

```csharp
var name = "Ada";
var copy = name;
copy += " Lovelace";
Console.WriteLine(name); // Ada
Console.WriteLine(copy); // Ada Lovelace
```

`==` on strings compares characters, not references. `ReferenceEquals(name, copy)` would be false after the concatenation.

Two more consequences:

1. **Default values.** `int` defaults to `0`, `bool` to `false`, reference-type fields to `null`. Local variables must be definitely assigned before use.
2. **Mutable structs are easy to misuse.** Passing a struct to a method copies it. If the method mutates the parameter, the caller's value is unchanged unless you use `ref`. This is why most modern structs are small and immutable.

The stack/heap distinction is an implementation detail. The useful rule is semantic: **value types copy their data; reference types share an object.**
""",
    workedExample={
        "title": "Predict the output of three assignments",
        "markdown": r"""
Read the program and predict every printed line before revealing the steps.

```csharp
var numbers = new[] { 10, 20 };
var alias = numbers;
alias[0] = 99;

var count = 1;
var otherCount = count;
otherCount = 5;

var greeting = "hi";
var shouted = greeting;
shouted = shouted.ToUpperInvariant();

Console.WriteLine(numbers[0]);
Console.WriteLine(alias[0]);
Console.WriteLine(count);
Console.WriteLine(greeting);
```
""",
        "steps": [
            "`numbers` and `alias` are reference variables. `alias = numbers` copies the reference, so both point to the same array object.",
            "`alias[0] = 99` mutates that shared array. Both `numbers[0]` and `alias[0]` are now `99`.",
            "`count` and `otherCount` are integers, a value type. `otherCount = count` copies `1`; changing `otherCount` to `5` cannot affect `count`.",
            "`greeting` and `shouted` initially reference the same string. `ToUpperInvariant()` creates a new string, and `shouted` is rebound to it. `greeting` still references `\"hi\"`.",
            "Output: `99`, `99`, `1`, `hi`."
        ]
    },
    explainPrompt="In your own words: why does changing a list through one variable show up through another, while changing an int through one variable does not?",
    checkQuestions=[
        q(
            "What is copied when a reference-type variable is assigned to another variable?",
            ["The entire object", "A reference to the same object", "A deep clone of every field", "Nothing; the assignment is ignored"],
            1,
            "Assignment copies the reference value. Both variables then point at the same object, which is why mutation is visible through both.",
            ["Objects are not copied on assignment for classes.", "Correct.", "A deep clone requires an explicit operation such as a copy constructor or serialization.", "Assignment does happen; it just copies the reference, not the object."],
        ),
        q(
            "Which statement about `string` is true?",
            ["It is a value type, so concatenation mutates the original", "It is a reference type and immutable, so operations create new strings", "It is mutable only inside a `foreach`", "It is a struct with a mutable char buffer"],
            1,
            "`string` is a reference type whose contents cannot change. Methods return new strings.",
            ["String is a reference type; immutable operations return new instances.", "Correct.", "Iteration has nothing to do with mutability.", "String is a sealed class, not a mutable struct."],
        ),
        q(
            "A method receives a `Point` struct parameter and sets `point.X = 10`. What does the caller see?",
            ["The caller's point is unchanged, because the struct was copied", "The caller's point is updated", "The program fails to compile", "The point becomes null"],
            0,
            "Parameters are passed by value by default. For a struct, that means a copy; the caller needs `ref` to see the change.",
            ["Correct: structs are copied on pass-by-value.", "That would require `ref` or `in`/`out` semantics with a mutable variable.", "Mutating a struct parameter is legal.", "Structs cannot be null unless wrapped in `Nullable<T>`."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-03-null-safety",
    topic="nullability",
    order=3,
    title="Null safety you can actually use",
    summary="Use nullable reference types and guard clauses to make 'no value' explicit instead of accidental.",
    estimatedMinutes=35,
    theory=r"""
`null` means “this variable does not refer to an object.” Calling a member on `null` throws `NullReferenceException`, one of the most common production failures.

Modern C# has **nullable reference types (NRT)**. They are a compile-time annotation system, not a runtime feature. With `<Nullable>enable</Nullable>` in the project:

- `string` means “the compiler expects a non-null reference here.”
- `string?` means “this may be null; check before dereferencing.”

The compiler warns when you assign `null` to a non-nullable variable, return a possibly-null value from a non-nullable method, or dereference a `string?` without checking. Warnings are guidance; they do not add runtime checks.

Useful tools:

```csharp
string? FindName(int id) => id == 1 ? "Ada" : null;

// 1. Pattern check narrows the type
var name = FindName(2);
if (name is not null)
    Console.WriteLine(name.Length);

// 2. Null-conditional and null-coalescing
var length = name?.Length ?? 0;
var display = name ?? "unknown";

// 3. Guard clauses at the boundary
void Greet(string name)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    Console.WriteLine($"Hello, {name}");
}
```

Use `!` (the null-forgiving operator) only when you can justify it. It silences the compiler without changing behavior. A common legitimate case is a field initialized by a framework, where you know initialization happens before use.

Distinguish three situations:

1. **Absence is normal** — model it explicitly with `string?`, `int?`, or a result type. Make callers handle it.
2. **Absence is a caller bug** — validate at the public boundary with `ArgumentNullException.ThrowIfNull`.
3. **Absence is impossible but the compiler cannot prove it** — annotate carefully, or restructure so it can.

NRT is most valuable at boundaries: public methods, API models, and data coming from files or the network. Inside a method, prefer early guards and non-null locals so the rest of the code reads cleanly.
""",
    workedExample={
        "title": "Make an unsafe lookup safe",
        "markdown": r"""
The original method throws on missing data and gives the caller no warning:

```csharp
static int LengthOf(string? text)
{
    return text.Length;
}
```

Read the fix, then the steps explaining why each part matters.
""",
        "steps": [
            "The parameter is `string?`, so the compiler knows `text` may be null. `text.Length` produces a warning and can throw at run time.",
            "Return a nullable result (`int?`) when absence has meaning, or define a deterministic fallback such as `0`. Do not silently pretend a missing string was empty unless that is truly the domain rule.",
            "Option A, explicit absence: `static int? LengthOf(string? text) => text?.Length;`",
            "Option B, fallback with a documented rule: `static int LengthOf(string? text) => text?.Length ?? 0;`",
            "Option C, caller bug: `static int LengthOf(string text) { ArgumentNullException.ThrowIfNull(text); return text.Length; }` — the signature now promises non-null.",
            "Whichever you choose, the type signature tells the truth. That is the core skill: make the possibility of null visible in the type, not hidden in a comment."
        ]
    },
    bugFix={
        "title": "The missing null check",
        "prompt": r"""
This method is supposed to return a trimmed display name or `"(anonymous)"` when no name was supplied. It throws a `NullReferenceException` for missing names.

```csharp
public static string DisplayName(string? name)
{
    return name.Trim().Length == 0 ? "(anonymous)" : name.Trim();
}
```

Find the bug, fix it, and explain why the compiler was trying to warn you.
""",
        "language": "csharp",
        "brokenCode": r"""public static string DisplayName(string? name)
{
    return name.Trim().Length == 0 ? "(anonymous)" : name.Trim();
}""",
        "explanation": "`name` may be null, so `name.Trim()` can throw before the length check ever runs. Guard first, then trim once: `if (string.IsNullOrWhiteSpace(name)) return \"(anonymous)\"; return name.Trim();`. The nullable annotation was correct; the code ignored it.",
    },
    explainPrompt="Explain the difference between a nullable reference type warning and a runtime null check. Why is `!` dangerous if used casually?",
    checkQuestions=[
        q(
            "What does `string?` mean in a nullable-enabled project?",
            ["The string is stored on the heap instead of the stack", "The reference may be null and callers should handle that", "The string can be modified", "The compiler inserts runtime null checks"],
            1,
            "`string?` is an annotation: the value may be absent. It does not change the runtime representation or insert checks.",
            ["All strings are reference types and live on the heap regardless of annotation.", "Correct.", "Mutability is unrelated; strings are immutable.", "No runtime checks are inserted by nullable reference types."],
        ),
        q(
            "Which expression provides a safe fallback when `name` may be null?",
            ["`name.Length`", "`name ?? \"unknown\"`", "`name!`", "`name.Value`"],
            1,
            "The null-coalescing operator returns the right operand when the left is null.",
            ["This dereferences null and can throw.", "Correct.", "The null-forgiving operator only silences warnings.", "`string` has no `.Value`; that belongs to nullable value types."],
        ),
        q(
            "When is the null-forgiving operator `!` appropriate?",
            ["Whenever the compiler warns, to keep the build clean", "When you have a real invariant the compiler cannot see, and you accept responsibility", "Only on value types", "As a replacement for `??`"],
            1,
            "`!` is an assertion. It is reasonable when you can justify the invariant, and dangerous when used to silence a genuine bug.",
            ["Silencing warnings without evidence hides bugs.", "Correct: it is an assertion of non-nullness.", "It applies to nullable reference and value types.", "It does not provide a fallback value."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-04-methods-and-parameters",
    topic="methods",
    order=4,
    title="Methods, parameters, and the ref/out distinction",
    summary="Understand exactly what a method receives, what it can change, and how to design clear signatures.",
    estimatedMinutes=40,
    theory=r"""
A method is a named block with a signature: name, parameter types, and return type. Overloading lets several methods share a name when their parameter lists differ.

```csharp
int Add(int a, int b) => a + b;
double Add(double a, double b) => a + b;
```

By default, arguments are passed **by value**. For a value type, that means a copy of the value; for a reference type, a copy of the reference. The method can mutate the object a reference points to, but it cannot reassign the caller's variable.

C# adds modifiers for the cases where copying is not what you want:

| Modifier | Meaning |
|---|---|
| `ref` | The caller's variable is passed by reference; the method may read and write it. It must be initialized first. |
| `out` | The method must assign the parameter before returning; used for multiple results and `Try...` patterns. |
| `in` | Read-only reference; avoids copying a large struct while promising not to modify it. |

```csharp
void Increment(ref int value) => value++;

bool TryParsePositive(string text, out int value)
{
    if (int.TryParse(text, out value) && value > 0)
        return true;
    value = 0;
    return false;
}
```

Call sites are explicit:

```csharp
var number = 4;
Increment(ref number);              // number is 5

if (TryParsePositive("42", out var parsed))
    Console.WriteLine(parsed);       // 42
```

Optional parameters must come after required ones. Named arguments make call sites readable, especially with booleans:

```csharp
void Configure(int retries = 3, bool verbose = false) { }
Configure(verbose: true);
```

Prefer returning a value over mutating through `ref` when both are reasonable. Use `out` for the `Try...` convention, `ref` when in-place mutation is genuinely the point, and `in` sparingly for large structs. Clear signatures come from choosing the least surprising mechanism.
""",
    workedExample={
        "title": "Build a safe parser with `out`",
        "markdown": r"""
We want a method that parses a positive integer and reports failure without throwing.

```csharp
static bool TryParsePositive(string text, out int value)
{
    value = 0;
    if (!int.TryParse(text, out var parsed))
        return false;
    if (parsed <= 0)
        return false;
    value = parsed;
    return true;
}
```

Trace the call `TryParsePositive("-3", out var n)`.
""",
        "steps": [
            "`value` is an `out` parameter. The method is required to assign it on every path, so it starts by assigning `0`.",
            "`int.TryParse(\"-3\", out var parsed)` succeeds: `parsed` is `-3`.",
            "The positivity check fails, so the method returns `false` without overwriting `value`.",
            "Back at the call site, `TryParsePositive` returns `false`. The variable `n` is definitely assigned to `0`, but a well-written caller only uses it when the result is `true`.",
            "The pattern separates success from the value, avoiding exceptions for expected invalid input. That is why `TryParse` exists alongside `Parse`."
        ]
    },
    task={
        "title": "Implement `TryParsePositive`",
        "prompt": r"""
Write a method with this signature:

```csharp
static bool TryParsePositive(string text, out int value)
```

It should return `true` and set `value` only when `text` is a valid integer greater than zero. Otherwise it returns `false` and sets `value` to `0`. Do not throw for invalid input.

Use the `out` parameter correctly on every path.
""",
        "language": "csharp",
        "starterCode": r"""static bool TryParsePositive(string text, out int value)
{
    // Your code here. Remember: out parameters must be assigned on every path.
    throw new NotImplementedException();
}""",
        "solution": r"""static bool TryParsePositive(string text, out int value)
{
    value = 0;
    return int.TryParse(text, out var parsed) && parsed > 0 && (value = parsed) > 0;
}""",
        "requirements": [
            "The method compiles with an out parameter assigned on every path.",
            "Invalid text, zero, and negative numbers return false.",
            "A valid positive number is returned through value.",
        ],
    },
    explainPrompt="Explain why `out` parameters must be assigned on every path, and when you would choose `ref` instead of returning a value.",
    checkQuestions=[
        q(
            "What does passing an argument by value mean for a reference type?",
            ["The method gets a copy of the object", "The method gets a copy of the reference, but can mutate the same object", "The method may reassign the caller's variable", "The object becomes immutable"],
            1,
            "The reference is copied, so the method can mutate the shared object through it, but reassigning the parameter affects only the copy of the reference.",
            ["Objects are not copied unless explicitly cloned.", "Correct.", "Reassigning the parameter only changes the local reference.", "Passing by value does not make the object immutable."],
        ),
        q(
            "Which statement about `out` parameters is required by the compiler?",
            ["They must be initialized by the caller", "They must be assigned by the method before it returns", "They can only be used with value types", "They must be the last parameter"],
            1,
            "The compiler enforces definite assignment on every return path for `out` parameters.",
            ["The caller does not need to initialize an `out` argument when using `out var`.", "Correct.", "They work with reference types too.", "They can appear anywhere, though convention places them last."],
        ),
        q(
            "Which call is valid for `void Configure(int retries = 3, bool verbose = false)`?",
            ["`Configure(verbose: true)`", "`Configure(true)`", "`Configure(verbose = true)`", "`Configure(retries: )`"],
            0,
            "Named arguments may skip optional parameters. The others are invalid syntax or wrong types.",
            ["Correct.", "The first parameter is `int`; a bool does not fit.", "This is an assignment expression, not named-argument syntax.", "A named argument still needs a value."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-05-classes-records-structs",
    topic="objects",
    order=5,
    title="Classes, records, and structs: choosing the right shape",
    summary="Model data with the semantics you actually want: identity, value equality, or a small copyable value.",
    estimatedMinutes=40,
    theory=r"""
C# gives you three main ways to define a type. The differences are semantic, not cosmetic.

**Classes** have reference identity. Two instances with equal field values are still different objects unless you override equality. Classes support inheritance and mutable state:

```csharp
public sealed class Lesson
{
    public string Id { get; }
    public string Title { get; set; }

    public Lesson(string id, string title)
    {
        Id = id;
        Title = title;
    }
}
```

**Records** are designed for data. A `record` (reference type) or `record struct` (value type) gets value equality, a readable `ToString`, and non-destructive mutation with `with`:

```csharp
public sealed record LessonNote(string LessonId, string Body, int Confidence);

var first = new LessonNote("arrays", "Indexing is O(1).", 4);
var revised = first with { Confidence = 5 };

Console.WriteLine(first == revised); // False, confidence differs
```

Positional records generate init-only properties and a constructor. They are immutable by default, which makes them safe to pass around and easy to reason about.

**Structs** are value types. Assigning or passing one copies it. They are a good fit for small, short-lived values such as coordinates, money amounts with an explicit currency, or a wrapper around a single primitive. They are a poor fit for large objects or types with reference identity. Keep them small and preferably immutable.

A useful decision guide:

- Does the thing have an identity that persists while its properties change? Use a **class**.
- Is it a snapshot of data where two equal values should be interchangeable? Use a **record**.
- Is it a tiny value with no identity, copied often, and cheap to copy? Consider a **struct** (or `record struct`).

Properties can be `init`-only, which allows object-initializer construction but prevents later mutation:

```csharp
public sealed class Track
{
    public required string SkillId { get; init; }
    public int DailyMinutes { get; init; } = 30;
}
```

`required` makes the compiler enforce that callers set `SkillId` during initialization. Both features reduce invalid objects without ceremony.
""",
    workedExample={
        "title": "Class identity vs record equality",
        "markdown": r"""
Compare two types that look similar but behave differently when compared.

```csharp
public sealed class NamedClass
{
    public string Name { get; init; } = "";
}

public sealed record NamedRecord(string Name);

var classA = new NamedClass { Name = "Ada" };
var classB = new NamedClass { Name = "Ada" };

var recordA = new NamedRecord("Ada");
var recordB = new NamedRecord("Ada");
```
""",
        "steps": [
            "`classA` and `classB` are two separate objects. `NamedClass` does not override `Equals`, so `classA == classB` is `false`: reference identity is compared.",
            "`recordA` and `recordB` have the same declared type and equal property values. Record equality is value-based, so `recordA == recordB` is `true`.",
            "`recordA with { Name = \"Grace\" }` creates a new record with the changed property. The original is untouched.",
            "If the class needs value equality, you must implement `Equals` and `GetHashCode` consistently, or switch to a record. Implementing equality by hand on a mutable class is error-prone.",
            "Choose based on meaning: a database row with a stable ID is often a class; a lesson note snapshot is naturally a record."
        ]
    },
    task={
        "title": "Model a lesson note as a record",
        "prompt": r"""
Create a `record` named `LessonNote` with three positional values:

- `LessonId` (string)
- `Body` (string)
- `Confidence` (int, 1 to 5)

Then write a method `WithConfidence(LessonNote note, int confidence)` that returns a new note with the confidence replaced, without mutating the original.
""",
        "language": "csharp",
        "starterCode": r"""public sealed record LessonNote(string LessonId, string Body, int Confidence);

public static LessonNote WithConfidence(LessonNote note, int confidence)
{
    // Return a copy with the new confidence.
    throw new NotImplementedException();
}""",
        "solution": r"""public sealed record LessonNote(string LessonId, string Body, int Confidence);

public static LessonNote WithConfidence(LessonNote note, int confidence)
{
    if (confidence is < 1 or > 5)
        throw new ArgumentOutOfRangeException(nameof(confidence));
    return note with { Confidence = confidence };
}""",
        "requirements": [
            "The original note is unchanged.",
            "The returned note has the same lesson id and body.",
            "Invalid confidence values are rejected.",
        ],
    },
    explainPrompt="When would you choose a class instead of a record for the same data? Describe a concrete case where value equality would be wrong.",
    checkQuestions=[
        q(
            "What does `with` do on a record?",
            ["Mutates the existing record in place", "Creates a copy with selected properties changed", "Deletes a property", "Converts the record to a class"],
            1,
            "`with` performs non-destructive mutation: it copies the record and applies the changes.",
            ["Records are immutable when positional; `with` does not mutate the original.", "Correct.", "It does not delete properties.", "The runtime type does not change."],
        ),
        q(
            "Two instances of a plain class with identical property values are compared with `==`. What is the result by default?",
            ["`true`, because values are equal", "`false`, because reference identity differs", "It throws", "It depends on the properties' order"],
            1,
            "Classes use reference equality unless `Equals` and `operator ==` are overridden.",
            ["Value equality must be implemented or use a record.", "Correct.", "Comparison does not throw for normal classes.", "Property order is irrelevant."],
        ),
        q(
            "Which is the best candidate for a struct?",
            ["A large object graph with shared identity", "A small immutable pair of coordinates", "A database entity with a mutable primary key", "A service that holds an HTTP client"],
            1,
            "Small immutable values with cheap copies suit structs. Large or identity-bearing objects are usually classes.",
            ["Shared identity is the domain of classes.", "Correct.", "Entities are usually classes with identity.", "Services belong in classes and are typically long-lived."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-06-interfaces-inheritance",
    topic="abstraction",
    order=6,
    title="Interfaces, inheritance, and composition",
    summary="Design seams in your code with contracts, and prefer composition when inheritance stops earning its keep.",
    estimatedMinutes=35,
    theory=r"""
An **interface** names a capability without supplying an implementation. Types that implement it promise to provide the members:

```csharp
public interface ILessonRenderer
{
    string Render(string title, string body);
}

public sealed class PlainTextRenderer : ILessonRenderer
{
    public string Render(string title, string body) => $"{title}\n\n{body}";
}
```

The caller can depend on `ILessonRenderer` rather than a concrete class. This is the basis of polymorphism and testability: in tests you can substitute a fake renderer without touching the production class.

An **abstract class** can contain state, constructors, and shared implementation. It expresses an “is a” relationship where some behavior is common:

```csharp
public abstract class LessonStep
{
    public abstract string Name { get; }
    public bool Completed { get; protected set; }
    public void MarkComplete() => Completed = true;
}
```

Use an interface when independent types should share a contract. Use an abstract base class when related types genuinely share implementation and identity.

**Default interface methods** let an interface provide a fallback implementation. They are useful for evolving libraries, but they are not a replacement for normal class design and cannot access instance state.

**Composition** means building behavior by holding collaborators:

```csharp
public sealed class LessonNotifier
{
    private readonly IEmailSender _email;
    public LessonNotifier(IEmailSender email) => _email = email;

    public Task NotifyAsync(string address) => _email.SendAsync(address, "Lesson ready");
}
```

This is usually clearer than a deep inheritance tree. Inheritance couples a derived class to its base's implementation details; composition lets you replace a part.

Two guidelines that prevent most design pain:

1. **Program to the smallest interface you need.** A consumer that only reads should not require a full repository with write methods.
2. **Do not create an interface for every class by reflex.** Introduce a seam when there is a real second implementation, a testing need, or a boundary such as I/O. Otherwise the abstraction adds indirection without value.
""",
    workedExample={
        "title": "Refactor a conditional into polymorphism",
        "markdown": r"""
This method grows every time a new output format is added:

```csharp
string Render(string format, string title, string body)
{
    if (format == "text") return $"{title}\n\n{body}";
    if (format == "html") return $"<h1>{title}</h1><p>{body}</p>";
    if (format == "markdown") return $"# {title}\n\n{body}";
    throw new NotSupportedException(format);
}
```

Model the varying behavior as a contract instead.
""",
        "steps": [
            "Identify the axis of change: the rendering format. Keep the stable data (`title`, `body`) as method parameters.",
            "Define `ILessonRenderer` with `string Render(string title, string body)`.",
            "Implement `PlainTextRenderer`, `HtmlRenderer`, and `MarkdownRenderer`. Each class owns exactly one format.",
            "The caller receives an `ILessonRenderer` and calls `Render` without a conditional. Adding a format means adding a class, not editing existing branches.",
            "Select the implementation once at composition time — for example in a factory or dependency-injection registration. The Open/Closed idea is a consequence, not the goal: the goal is local, understandable change."
        ]
    },
    explainPrompt="Explain the difference between an interface and an abstract class. Then describe a situation where composition is clearly better than inheritance.",
    checkQuestions=[
        q(
            "What can an abstract class do that a C# interface cannot (without default members)?",
            ["Declare method signatures", "Store instance fields and share implementation", "Be implemented by multiple types", "Be used as a parameter type"],
            1,
            "Abstract classes can hold state and implementation. Interfaces traditionally declare contracts only, though default interface methods now allow limited implementation.",
            ["Both can declare members.", "Correct.", "Both can be implemented or derived by multiple types.", "Both can be used as parameter types."],
        ),
        q(
            "Why is depending on an interface useful for testing?",
            ["It makes the program run faster", "A fake implementation can replace real I/O in a test", "It removes the need for constructors", "It makes all methods static"],
            1,
            "A contract allows substituting a test double, isolating the code under test from slow or unreliable dependencies.",
            ["Interfaces do not inherently improve runtime speed.", "Correct.", "Interfaces do not affect constructor requirements.", "Interfaces have nothing to do with static methods."],
        ),
        q(
            "What is a sign that composition may be better than inheritance?",
            ["You want to reuse one method from a base class", "The derived class is not genuinely a subtype and only needs some behavior", "The base class has no methods", "You need an abstract class"],
            1,
            "Inheritance should model an is-a relationship with substitutability. Reusing a fragment of behavior is often better handled by a collaborator.",
            ["Reusing one method is a common misuse of inheritance; a collaborator is usually cleaner.", "Correct.", "An empty base class provides no reason for inheritance.", "Abstract classes are one tool, not a goal."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-07-generics",
    topic="generics",
    order=7,
    title="Generics: type safety without duplication",
    summary="Write once, keep compile-time types, and understand what constraints let the compiler guarantee.",
    estimatedMinutes=40,
    theory=r"""
Generics let code work with many types while preserving compile-time type information. Without generics, you either duplicate code or fall back to `object`, which loses safety and can cause boxing.

```csharp
public static T First<T>(IReadOnlyList<T> items)
{
    if (items.Count == 0) throw new InvalidOperationException("The list is empty.");
    return items[0];
}

var number = First(new[] { 10, 20 });      // T is int
var name = First(new[] { "Ada", "Grace" }); // T is string
```

The compiler infers `T` from the argument and enforces the return type. No cast is required.

**Constraints** describe what `T` must support:

```csharp
public static T Max<T>(IEnumerable<T> values) where T : IComparable<T>
{
    using var iterator = values.GetEnumerator();
    if (!iterator.MoveNext()) throw new InvalidOperationException("Empty sequence.");

    var best = iterator.Current;
    while (iterator.MoveNext())
        if (iterator.Current.CompareTo(best) > 0)
            best = iterator.Current;
    return best;
}
```

Common constraints:

- `where T : class` — reference type
- `where T : struct` — non-nullable value type
- `where T : notnull` — excludes nullable types
- `where T : new()` — must have a public parameterless constructor
- `where T : IComparable<T>` — must implement an interface
- `where T : BaseClass` — must derive from a base class

A generic type is the same idea for classes:

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool success, T? value, string? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

Generics also avoid boxing. `List<int>` stores `int`s directly; a non-generic `ArrayList` stores `object` references and boxes each integer. For hot paths this matters.

Variance is a more advanced topic. `IEnumerable<out T>` is covariant: an `IEnumerable<string>` can be used where `IEnumerable<object>` is expected, because it only produces values. Mutable collections cannot safely be covariant.
""",
    workedExample={
        "title": "Constraint-driven generics",
        "markdown": r"""
We want a method that returns the largest value in a non-empty sequence.

```csharp
static T Max<T>(IEnumerable<T> values) where T : IComparable<T>
{
    using var e = values.GetEnumerator();
    if (!e.MoveNext()) throw new InvalidOperationException("Empty sequence.");

    var best = e.Current;
    while (e.MoveNext())
    {
        if (e.Current.CompareTo(best) > 0)
            best = e.Current;
    }
    return best;
}
```
""",
        "steps": [
            "The constraint `IComparable<T>` tells the compiler that every `T` has `CompareTo(T)`. Without it, `CompareTo` would not compile.",
            "`GetEnumerator()` works for any `IEnumerable<T>`: arrays, lists, and lazy sequences. The method does not need to know which one.",
            "`MoveNext()` advances once. The first call checks for an empty sequence and initializes `best`.",
            "Each later value is compared with `best`; the larger one wins. No boxing occurs for value types because `T` is concrete at the call site.",
            "`Max(new[] { 3, 9, 4 })` infers `T = int`. `Max(new[] { \"b\", \"a\" })` infers `T = string`, and string implements `IComparable<string>`."
        ]
    },
    explainPrompt="Why do generics preserve type safety better than using `object`? Give an example involving boxing or a cast.",
    checkQuestions=[
        q(
            "What does `where T : IComparable<T>` guarantee inside a generic method?",
            ["`T` is a reference type", "Every `T` has a `CompareTo(T)` method", "`T` has a parameterless constructor", "`T` is an integer"],
            1,
            "The constraint exposes members of the interface on values of type `T`.",
            ["That would require `where T : class`.", "Correct.", "That requires `new()`.", "`IComparable<T>` does not restrict `T` to integers."],
        ),
        q(
            "Why is `List<int>` generally better than a non-generic `ArrayList` for integers?",
            ["It avoids boxing and keeps compile-time type safety", "It uses less memory because it stores strings", "It can hold more than one type", "It runs on more operating systems"],
            0,
            "Generic collections store values without boxing and expose typed members.",
            ["Correct.", "`List<int>` stores integers, not strings.", "`List<int>` is intentionally typed to one type parameter.", "Both are part of .NET and platform support is unrelated."],
        ),
        q(
            "Which constraint would let a generic method call `new T()`?",
            ["`where T : class`", "`where T : struct`", "`where T : new()`", "`where T : notnull`"],
            2,
            "`new()` requires a public parameterless constructor, enabling `new T()`.",
            ["`class` allows reference types but not necessarily a parameterless constructor.", "`struct` implies a default constructor but does not allow the `new T()` constraint usage in the same way; the explicit constraint is `new()`.", "Correct.", "`notnull` says nothing about constructors."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-08-collections",
    topic="collections",
    order=8,
    title="Collections and IEnumerable",
    summary="Pick the right collection for the access pattern, and understand when enumeration is lazy.",
    estimatedMinutes=40,
    theory=r"""
C# collections differ in the operations they make cheap. Choosing well is a design decision, not a matter of habit.

| Type | Best at | Watch out for |
|---|---|---|
| `T[]` | Fixed-size, index access | Cannot grow |
| `List<T>` | Append, index access, iteration | Removing from the middle is O(n) |
| `Dictionary<TKey,TValue>` | Key lookup, insert, delete | Unordered; keys must have sensible equality |
| `HashSet<T>` | Membership and uniqueness | No indexing |
| `Queue<T>` | First-in, first-out | No random access |
| `Stack<T>` | Last-in, first-out | No random access |

`List<T>` is backed by an array. When it runs out of capacity, it allocates a larger array and copies. That is why appending is *amortized* O(1), while an individual append can be O(n).

```csharp
var lessons = new List<string> { "types", "collections" };
lessons.Add("linq");
foreach (var lesson in lessons)
    Console.WriteLine(lesson);
```

`IEnumerable<T>` is the abstraction for “something you can iterate.” It is lazy: a LINQ query does not execute until you enumerate it.

```csharp
IEnumerable<int> Evens(IEnumerable<int> source)
{
    foreach (var n in source)
        if (n % 2 == 0) yield return n;
}

var numbers = new List<int> { 1, 2, 3, 4 };
var evens = Evens(numbers); // nothing has run yet
numbers.Add(6);
foreach (var n in evens) Console.WriteLine(n); // 2, 4, 6
```

The iterator sees the list at enumeration time. This is powerful, but it also means:

- A sequence can change between creation and enumeration.
- Enumerating the same lazy query twice can perform the work twice.
- You cannot modify a collection while a `foreach` is enumerating it. `List<T>` throws `InvalidOperationException` when its version changes.

To mutate while iterating, iterate over a copy or collect the items to remove first:

```csharp
foreach (var lesson in lessons.ToList())
    if (lesson.StartsWith("old"))
        lessons.Remove(lesson);
```

Use `IReadOnlyList<T>` in method parameters when you only read. Return the most general type the caller needs: `IReadOnlyList<T>` for indexed reading, `IEnumerable<T>` for streaming.
""",
    workedExample={
        "title": "Remove while iterating, safely",
        "markdown": r"""
This code looks reasonable, but it throws when it removes the first item:

```csharp
var topics = new List<string> { "old-basics", "arrays", "old-notes", "strings" };
foreach (var topic in topics)
{
    if (topic.StartsWith("old"))
        topics.Remove(topic);
}
```
""",
        "steps": [
            "`foreach` asks the list for an enumerator. The enumerator tracks a version number.",
            "When `Remove` changes the list, the version changes. The next `MoveNext` detects the mismatch and throws `InvalidOperationException`.",
            "Removing during enumeration is not a quirk of `List<T>`; it protects you from skipping or revisiting elements unpredictably.",
            "Fix one: iterate over a snapshot: `foreach (var topic in topics.ToList())`. Removing from `topics` no longer affects the enumerator over the copy.",
            "Fix two: remove by index from the end, or use `RemoveAll(t => t.StartsWith(\"old\"))`, which is explicit about mutating the collection."
        ]
    },
    bugFix={
        "title": "The collection that changed while being read",
        "prompt": r"""
This method is meant to delete every completed lesson. It crashes on the first removal.

```csharp
public static void RemoveCompleted(List<string> lessonIds, HashSet<string> completed)
{
    foreach (var id in lessonIds)
    {
        if (completed.Contains(id))
            lessonIds.Remove(id);
    }
}
```

Fix it and explain the exception.
""",
        "language": "csharp",
        "brokenCode": r"""public static void RemoveCompleted(List<string> lessonIds, HashSet<string> completed)
{
    foreach (var id in lessonIds)
    {
        if (completed.Contains(id))
            lessonIds.Remove(id);
    }
}""",
        "explanation": "The `foreach` enumerator is invalidated when the list is modified, so the next `MoveNext` throws `InvalidOperationException`. Use `lessonIds.RemoveAll(completed.Contains)`, or iterate over a snapshot such as `lessonIds.ToList()`. `RemoveAll` is preferable here because it states the intent and performs one pass.",
    },
    explainPrompt="Explain deferred execution with `IEnumerable<T>`. What can go wrong if you enumerate the same query twice?",
    checkQuestions=[
        q(
            "Which collection is the best default for fast lookup by a unique key?",
            ["`List<T>`", "`Dictionary<TKey,TValue>`", "`Queue<T>`", "`T[]`"],
            1,
            "A dictionary provides average O(1) lookup by key. A list requires a scan.",
            ["A list lookup is O(n) unless you know the index.", "Correct.", "A queue is for FIFO processing, not key lookup.", "An array lookup by index is O(1), but finding by key requires a scan."],
        ),
        q(
            "Why does modifying a `List<T>` inside `foreach` throw?",
            ["The list becomes read-only", "The enumerator detects a version change and invalidates itself", "The loop variable is immutable", "`foreach` copies the entire list"],
            1,
            "List enumerators check a version field; mutation invalidates the enumerator to prevent undefined traversal.",
            ["The list is not made read-only.", "Correct.", "The loop variable is read-only, but that is a separate rule.", "`foreach` does not copy the list."],
        ),
        q(
            "When does a LINQ `Where` query over a list actually filter items?",
            ["When `Where` is called", "When the query is enumerated", "When the source list is constructed", "At compile time"],
            1,
            "LINQ operators build a pipeline. Execution is deferred until enumeration or materialization.",
            ["`Where` creates an iterator; it does not filter immediately.", "Correct.", "The source is independent of the query.", "Filtering is a runtime operation."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-09-linq",
    topic="linq",
    order=9,
    title="LINQ: composing queries without leaving C#",
    summary="Use deferred pipelines deliberately, keep queries readable, and know when to materialize.",
    estimatedMinutes=40,
    theory=r"""
LINQ is a set of extension methods over `IEnumerable<T>` (and `IQueryable<T>`). It lets you express filtering, projection, ordering, grouping, and aggregation as a pipeline:

```csharp
var summaries = lessons
    .Where(l => l.Order > 3)
    .OrderBy(l => l.Title)
    .Select(l => $"{l.Order:00} — {l.Title}")
    .ToList();
```

The method chain is equivalent to query syntax, which some people find clearer for joins and grouping:

```csharp
var summaries =
    from lesson in lessons
    where lesson.Order > 3
    orderby lesson.Title
    select $"{lesson.Order:00} — {lesson.Title}";
```

Most LINQ is **deferred**. `Where`, `Select`, `OrderBy`, and `GroupBy` build a description of the work; nothing runs until you enumerate. `Count`, `Any`, `First`, `ToArray`, `ToList`, and `Sum` force execution. This has practical consequences:

- A query can see changes made to the source after it was created.
- Enumerating the same query twice can repeat expensive work. Materialize once if you need the results repeatedly.
- `First()` on an empty sequence throws; `FirstOrDefault()` returns `default` (which may be null). Prefer `Any()` or a nullable result when absence is expected.
- `OrderBy` is stable in LINQ to Objects, but do not rely on stability through databases without checking the provider.

Operator families worth knowing:

| Goal | Operators |
|---|---|
| Filter / project | `Where`, `Select`, `SelectMany` |
| Order / page | `OrderBy`, `ThenBy`, `Skip`, `Take` |
| Aggregate | `Count`, `Sum`, `Average`, `Min`, `Max`, `Aggregate` |
| Test | `Any`, `All`, `Contains` |
| Shape | `GroupBy`, `Distinct`, `Zip`, `Chunk` |
| Join | `Join`, `GroupJoin`, `SelectMany` |

Prefer a clear pipeline over a clever one. If a lambda needs a comment to explain its structure, a helper method or an explicit loop may be more readable. LINQ is not a performance guarantee; know the source (in-memory collection vs database query) and measure when it matters.
""",
    workedExample={
        "title": "Build a study summary",
        "markdown": r"""
Given a list of lessons, produce the titles of unfinished lessons in order, grouped by topic, with a count per topic.

```csharp
public sealed record LessonRow(string Title, string Topic, bool Completed);

var rows = new[]
{
    new LessonRow("Types", "fundamentals", true),
    new LessonRow("Null safety", "nullability", false),
    new LessonRow("Generics", "generics", false),
    new LessonRow("LINQ", "linq", false)
};

var summary = rows
    .Where(r => !r.Completed)
    .OrderBy(r => r.Topic)
    .GroupBy(r => r.Topic)
    .Select(g => new
    {
        Topic = g.Key,
        Count = g.Count(),
        Titles = g.Select(r => r.Title).ToList()
    })
    .ToList();
```
""",
        "steps": [
            "`Where(r => !r.Completed)` keeps nullability, generics, and LINQ. The pipeline is deferred at this point.",
            "`OrderBy(r => r.Topic)` defines an ordering. Ordering must materialize its input to sort, but the whole query is still not executed until final enumeration.",
            "`GroupBy` produces one group per topic. Within each group, the original relative order is preserved for LINQ to Objects.",
            "`Select` shapes each group into a summary object with a count and the matching titles.",
            "`ToList()` forces execution once. Without it, each enumeration of `summary` would rerun the entire pipeline.",
            "The result has three groups, each with one item in this example. If you add another unfinished generics lesson, the generics count becomes 2."
        ]
    },
    task={
        "title": "Filter and rank study items",
        "prompt": r"""
Given a sequence of `StudyItem(string Title, int Minutes, int Priority)`, return the titles of the three items with the highest priority, breaking ties by fewer minutes. Only include items with `Priority >= 3`. Return `IReadOnlyList<string>`.

Write the query and materialize it once.
""",
        "language": "csharp",
        "starterCode": r"""public sealed record StudyItem(string Title, int Minutes, int Priority);

public static IReadOnlyList<string> TopStudyItems(IEnumerable<StudyItem> items)
{
    // Your LINQ pipeline here.
    throw new NotImplementedException();
}""",
        "solution": r"""public sealed record StudyItem(string Title, int Minutes, int Priority);

public static IReadOnlyList<string> TopStudyItems(IEnumerable<StudyItem> items)
{
    return items
        .Where(i => i.Priority >= 3)
        .OrderByDescending(i => i.Priority)
        .ThenBy(i => i.Minutes)
        .Take(3)
        .Select(i => i.Title)
        .ToList();
}""",
        "requirements": [
            "Items below priority 3 are excluded.",
            "Higher priority comes first; equal priority puts shorter items first.",
            "At most three titles are returned.",
            "The result is materialized exactly once.",
        ],
    },
    explainPrompt="Explain deferred execution using one LINQ operator that is deferred and one that forces execution. What happens if you enumerate a deferred query twice?",
    checkQuestions=[
        q(
            "Which call forces a LINQ query to execute immediately?",
            ["`Where`", "`Select`", "`ToList`", "`OrderBy`"],
            2,
            "`ToList` enumerates the sequence and stores the results. `Where` and `Select` are lazy; `OrderBy` builds an ordered iterator that still waits for enumeration.",
            ["`Where` returns a lazy iterator.", "`Select` returns a lazy iterator.", "Correct.", "`OrderBy` builds an iterator but does not materialize until enumerated."],
        ),
        q(
            "What does `FirstOrDefault()` return for an empty sequence of reference type?",
            ["An empty string", "`null`", "It throws `InvalidOperationException`", "A new instance"],
            1,
            "The default for a reference type is null. Use `First()` if absence should throw, or handle the null explicitly.",
            ["`default(string)` is null, not an empty string.", "Correct.", "`First()` throws; `FirstOrDefault()` does not.", "No instance is created automatically."],
        ),
        q(
            "Why is enumerating the same deferred query twice potentially expensive?",
            ["The query is cached forever", "The pipeline re-runs against the source each time", "LINQ locks the source on first use", "The second enumeration always throws"],
            1,
            "Unless materialized, each enumeration executes the operators again.",
            ["No permanent cache is created by default.", "Correct.", "Normal LINQ does not lock the source.", "Re-enumeration is allowed but repeats work."],
        ),
    ],
))

lessons.append(lesson(
    id="csharp-10-async-await",
    topic="async",
    order=10,
    title="async/await without blocking",
    summary="Model I/O as asynchronous work, keep the UI responsive, and avoid the classic deadlocks.",
    estimatedMinutes=45,
    theory=r"""
Asynchronous code exists to avoid holding a thread while waiting for I/O: a network call, a file read, a database query. `async`/`await` lets you write that code in a sequential style.

```csharp
public async Task<string> FetchLessonAsync(HttpClient http, string url, CancellationToken ct)
{
    using var response = await http.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
}
```

Key facts:

- An `async` method usually returns `Task`, `Task<T>`, or `ValueTask<T>`. `async void` is only for event handlers.
- `await` asynchronously waits. It does **not** block the calling thread; the method is suspended and resumed when the awaited task completes.
- Before the await completes, the method returns a `Task` to its caller. The caller can do other work or await it.
- Exceptions in an awaited task are rethrown at the `await`. Unobserved exceptions in fire-and-forget tasks are dangerous.
- Use `Task.WhenAll` to run independent operations concurrently:

```csharp
var first = FetchLessonAsync(http, url1, ct);
var second = FetchLessonAsync(http, url2, ct);
var texts = await Task.WhenAll(first, second);
```

Do not start a task and then block on it with `.Result` or `.Wait()`. In UI and older ASP.NET contexts this can deadlock because the continuation needs the context that the blocked thread is holding. Await instead. In modern ASP.NET Core there is no synchronization context, but blocking still wastes threads and can cause starvation under load.

**Cancellation** is cooperative. Pass a `CancellationToken` through I/O calls. Do not swallow `OperationCanceledException` unless you are deliberately treating cancellation as success.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var text = await FetchLessonAsync(http, url, cts.Token);
```

Async is not automatically parallel. Two sequential `await`s still run one after another. Start independent tasks before awaiting when concurrency is safe, and remember that shared mutable state needs synchronization in either case.

A practical rule: async all the way up. Once a method does I/O, let its callers be async rather than hiding the wait behind `.Result`.
""",
    workedExample={
        "title": "Sequential vs concurrent awaits",
        "markdown": r"""
Compare two versions of a method that fetches two lessons.

```csharp
// Version A — sequential
var a = await FetchAsync("a");
var b = await FetchAsync("b");

// Version B — concurrent
var taskA = FetchAsync("a");
var taskB = FetchAsync("b");
var results = await Task.WhenAll(taskA, taskB);
```
""",
        "steps": [
            "In version A, the first await suspends until `a` completes. Only then does the method start fetching `b`. Total time is roughly the sum of both calls.",
            "In version B, `FetchAsync(\"a\")` begins and returns a `Task<string>` immediately; the method does not await yet. The same happens for `b`.",
            "`Task.WhenAll` waits for both tasks and returns their results in input order. The two I/O operations overlap, so total time is roughly the slower of the two.",
            "If either task throws, the awaited `WhenAll` throws, exposing one of the exceptions. Use `await Task.WhenAll` and handle failures deliberately.",
            "Concurrency is appropriate only when the operations are independent and the downstream system can handle the load. Do not fire unbounded parallelism."
        ]
    },
    bugFix={
        "title": "The blocking call inside async",
        "prompt": r"""
This method is asynchronous, but it can deadlock in a UI app and it defeats the purpose of async. Find the problem.

```csharp
public string LoadProfile(int userId)
{
    var response = _http.GetStringAsync($"/users/{userId}").Result;
    return response;
}
```
""",
        "language": "csharp",
        "brokenCode": r"""public string LoadProfile(int userId)
{
    var response = _http.GetStringAsync($"/users/{userId}").Result;
    return response;
}""",
        "explanation": "`.Result` blocks the current thread while the I/O is in flight. In a synchronization-context environment, the continuation may need that same thread, producing a deadlock; even without a deadlock, a thread is wasted. Make the method `async Task<string>` and `await` the call: `public async Task<string> LoadProfileAsync(int userId, CancellationToken ct = default) => await _http.GetStringAsync($\"/users/{userId}\", ct);`. Async should flow through the call chain.",
    },
    explainPrompt="Explain what `await` does to the current thread, and why `.Result` on an async task is dangerous. When would you use `Task.WhenAll`?",
    checkQuestions=[
        q(
            "What happens when an async method awaits an incomplete I/O task?",
            ["The thread blocks until the task finishes", "The method returns a Task to its caller and resumes later", "The runtime creates a new OS thread for every await", "The task runs synchronously on the same thread"],
            1,
            "`await` suspends the method and returns control to the caller; a continuation resumes the method when the operation completes.",
            ["Awaiting is non-blocking for the calling thread.", "Correct.", "A new thread is not created per await; continuations are scheduled when needed.", "I/O completion does not require the same thread to sit and wait."],
        ),
        q(
            "Why is `.Result` dangerous in async code?",
            ["It returns the wrong type", "It blocks a thread and can deadlock with a synchronization context", "It cancels the task", "It only works for value types"],
            1,
            "Blocking on an async operation can deadlock and causes thread-pool starvation under load.",
            ["The type is usually correct; the waiting behavior is the problem.", "Correct.", "It does not cancel anything.", "`Task<T>.Result` works for all `T`."],
        ),
        q(
            "When is `Task.WhenAll` a good choice?",
            ["When operations are independent and can safely run concurrently", "When each operation depends on the previous result", "When you want to ignore exceptions", "When you need a synchronous return value"],
            0,
            "`WhenAll` overlaps independent asynchronous operations and waits for all of them.",
            ["Correct.", "Dependent operations must be awaited in sequence.", "`WhenAll` surfaces failures; it does not hide exceptions.", "It returns an awaitable, not a synchronous value."],
        ),
    ],
))

for item in lessons:
    path = ROOT / f"{item['order']:02d}-{item['id']}.json"
    path.write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding="utf-8")
    print(path.name)

print(f"Wrote {len(lessons)} C# lessons to {ROOT}")
