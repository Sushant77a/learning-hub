import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1] / "curriculum" / "dsa"
ROOT.mkdir(parents=True, exist_ok=True)

def q(prompt, options, correct, explanation, option_explanations):
    return {
        "prompt": prompt,
        "options": options,
        "correctIndex": correct,
        "explanation": explanation,
        "optionExplanations": option_explanations,
    }

def complexity(prompt, code, answer, explanation, language="csharp"):
    return {
        "prompt": prompt,
        "code": code,
        "language": language,
        "answer": answer,
        "explanation": explanation,
    }

def practice(title, prompt, hints, solution):
    return {"title": title, "prompt": prompt, "hints": hints, "solution": solution}

lessons = []

lessons.append({
    "id": "dsa-01-big-o",
    "skill": "dsa",
    "topic": "big-o",
    "order": 1,
    "title": "Big-O: reasoning about growth",
    "summary": "Count the work an algorithm does as input grows, then keep only what dominates.",
    "estimatedMinutes": 45,
    "theory": r"""
Big-O describes how an algorithm's cost **grows** as the input size grows. It is not a stopwatch measurement. It answers: if I double `n`, roughly what happens to the work?

We count the dominant operations — comparisons, assignments, array accesses, loop iterations — as a function of `n`, then discard constants and lower-order terms.

```csharp
int Sum(int[] values)
{
    var total = 0;                 // 1
    foreach (var value in values) // n iterations
        total += value;            // constant per iteration
    return total;                  // 1
}
```

The exact count is `n + 2` operations. Big-O keeps the fastest-growing term and drops constants: **O(n)**.

Common growth rates, from better to worse:

| Complexity | Name | Typical example |
|---|---|---|
| O(1) | constant | array index, hash lookup (average) |
| O(log n) | logarithmic | binary search, balanced tree lookup |
| O(n) | linear | one pass over an array |
| O(n log n) | linearithmic | comparison sorting |
| O(n²) | quadratic | nested loops over the same input |
| O(2ⁿ) | exponential | naive subset enumeration |

Rules of thumb:

1. **Sequential loops add.** Two separate loops of `n` are O(n), not O(2n) in Big-O — the constant disappears.
2. **Nested loops multiply.** A loop of `n` containing a loop of `m` is O(n · m). If both are `n`, it is O(n²).
3. **Halving the input each step is logarithmic.** Binary search throws away half the remaining range each time, giving O(log n).
4. **The input matters.** A loop over every element of a string of length `n` is O(n) regardless of how many bytes each character takes.

Big-O usually describes the **worst case**, but not always. Hash map lookup is average O(1) and worst-case O(n) when many keys collide. Quicksort is average O(n log n) and worst-case O(n²). State which case you mean.

**Space complexity** counts extra memory beyond the input. A single accumulator is O(1) extra space. Copying the array is O(n). Recursion uses stack space proportional to the depth of calls.

The skill is not memorizing a table. It is reading code and asking: *which loop depends on the input size, and how many times does the work happen?*
""",
    "workedExample": {
        "title": "Count a nested loop",
        "markdown": r"""
```csharp
int CountPairs(int[] values)
{
    var count = 0;
    for (var i = 0; i < values.Length; i++)
    {
        for (var j = i + 1; j < values.Length; j++)
        {
            if (values[i] == values[j])
                count++;
        }
    }
    return count;
}
```

Determine the time and space complexity.
""",
        "steps": [
            "Let `n = values.Length`. The outer loop runs `n` times.",
            "For a fixed `i`, the inner loop runs from `i + 1` to `n - 1`, which is `n - 1 - i` iterations.",
            "Total iterations: `(n - 1) + (n - 2) + ... + 1 + 0 = n(n - 1)/2`.",
            "Expand: `(n² - n)/2`. The dominant term is `n²`; constants and lower-order terms are dropped. Time complexity: **O(n²)**.",
            "The only extra variables are `i`, `j`, and `count`; no array or collection is allocated. Extra space: **O(1)**.",
            "Note that the loop still performs the full pair scan even after it could stop early. Big-O describes the code as written, including that wasted work."
        ]
    },
    "explainPrompt": "Explain why two separate O(n) loops are O(n) rather than O(n²), and why a nested loop over the same data is usually O(n²).",
    "checkQuestions": [
        q(
            "What is the time complexity of a single loop over an array of length `n` that performs constant work per iteration?",
            ["O(1)", "O(log n)", "O(n)", "O(n²)"],
            2,
            "The work grows linearly with the number of elements.",
            ["Constant time means the work does not depend on `n`.", "O(log n) would require halving the work each step.", "Correct.", "That would require nested loops over the input."],
        ),
        q(
            "Which of these is the correct simplification of `3n² + 5n + 7` in Big-O?",
            ["O(n²)", "O(3n² + 5n)", "O(n)", "O(1)"],
            0,
            "Big-O keeps the fastest-growing term and drops constant factors and lower-order terms.",
            ["Correct: `n²` dominates as `n` grows.", "This is not simplified; constants and lower-order terms are removed.", "`n²` grows faster than `n`.", "The expression depends on `n`, so it is not constant."],
        ),
        q(
            "What does O(1) extra space mean?",
            ["The algorithm uses no memory at all", "The extra memory does not grow with the input size", "The input is copied once", "The algorithm uses exactly one variable"],
            1,
            "O(1) space allows a fixed amount of extra state, independent of `n`.",
            ["Every function uses some memory; O(1) means a constant amount.", "Correct.", "Copying the input would be O(n) extra space.", "It could use several variables, as long as the count is fixed."],
        ),
    ],
    "dsa": {
        "topic": "big-o",
        "complexityQuestions": [
            complexity(
                "State the time and space complexity of this method.",
                r"""int Max(int[] values)
{
    var best = values[0];
    for (var i = 1; i < values.Length; i++)
    {
        if (values[i] > best)
            best = values[i];
    }
    return best;
}""",
                "Time: O(n), Space: O(1)",
                "The loop visits each element once, so time is linear. Only `best` and `i` are allocated, so extra space is constant.",
            ),
            complexity(
                "State the time and space complexity of building this frequency table.",
                r"""Dictionary<int, int> CountValues(int[] values)
{
    var counts = new Dictionary<int, int>();
    foreach (var value in values)
    {
        counts[value] = counts.GetValueOrDefault(value) + 1;
    }
    return counts;
}""",
                "Time: O(n) average, Space: O(n)",
                "Each insertion/lookup is O(1) on average, for n elements. In the worst case of many hash collisions, lookups can degrade toward O(n) per operation, making the total O(n²). Extra space is O(n) for up to n distinct keys.",
            ),
        ],
        "practice": [
            practice(
                "Contains a duplicate",
                r"""
Given an integer array `values`, return `true` if any value appears more than once. Otherwise return `false`.

```csharp
bool ContainsDuplicate(int[] values)
```

Aim for O(n) time. Explain why a sorted-input solution has a different trade-off.
""",
                [
                    "Start with the straightforward approach: compare every pair. What complexity does that give?",
                    "Can a data structure tell you whether you have seen a value before in roughly constant time? A `HashSet<int>` can.",
                ],
                r"""bool ContainsDuplicate(int[] values)
{
    var seen = new HashSet<int>();
    foreach (var value in values)
    {
        if (!seen.Add(value))
            return true;
    }
    return false;
}""",
            ),
            practice(
                "Count pairs with a target sum",
                r"""
Given a sorted array `values` and an integer `target`, count how many pairs `(i, j)` with `i < j` satisfy `values[i] + values[j] == target`.

```csharp
int CountPairs(int[] values, int target)
```

Target O(n) time and O(1) extra space, exploiting the sorted order.
""",
                [
                    "With two indices at opposite ends, what happens to the sum if you move the left index right? What if you move the right index left?",
                    "If the current sum is too small, the left index must move right. If it is too large, the right index must move left. What should happen on equality, and how do duplicates affect the count?",
                ],
                r"""int CountPairs(int[] values, int target)
{
    var left = 0;
    var right = values.Length - 1;
    var count = 0;

    while (left < right)
    {
        var sum = values[left] + values[right];
        if (sum < target) left++;
        else if (sum > target) right--;
        else
        {
            var leftValue = values[left];
            var rightValue = values[right];
            var leftCount = 0;
            while (left < right && values[left] == leftValue) { left++; leftCount++; }

            var rightCount = 0;
            if (leftValue == rightValue)
            {
                // All remaining equal values pair with each other.
                count += leftCount * (leftCount - 1) / 2;
                break;
            }
            while (left <= right && values[right] == rightValue) { right--; rightCount++; }
            count += leftCount * rightCount;
        }
    }
    return count;
}""",
            ),
        ],
    },
})

lessons.append({
    "id": "dsa-02-arrays",
    "skill": "dsa",
    "topic": "arrays",
    "order": 2,
    "title": "Arrays: direct access and expensive shifts",
    "summary": "Understand why indexing is O(1), why insertion is not, and how two pointers exploit layout.",
    "estimatedMinutes": 45,
    "theory": r"""
An array is a contiguous block of elements of the same type. Because every element has the same size, the address of element `i` is computed directly:

```
address(i) = baseAddress + i * elementSize
```

That is why reading or writing `values[i]` is **O(1)**. The machine does not scan from the start; it computes the address.

Arrays have a fixed length. In C#, `int[]` is the low-level contiguous array. `List<T>` wraps an array and grows it: when full, it allocates a larger array (typically doubling) and copies the elements. Appending is amortized O(1), but an individual growth event is O(n).

Costs to remember:

| Operation | Array | Dynamic array (`List<T>`) |
|---|---|---|
| Read/write by index | O(1) | O(1) |
| Append at end | — | Amortized O(1) |
| Insert/remove at front or middle | O(n) | O(n) |
| Search unsorted | O(n) | O(n) |
| Search sorted (binary search) | O(log n) | O(log n) |

Inserting at position `k` requires shifting every element after `k` one slot to the right. Removing requires shifting left. This is why linked structures exist, and why bulk operations are often cheaper than repeated single inserts.

**Two pointers** is a pattern that uses the array layout and ordering:

```csharp
bool HasPairSum(int[] sorted, int target)
{
    var left = 0;
    var right = sorted.Length - 1;
    while (left < right)
    {
        var sum = sorted[left] + sorted[right];
        if (sum == target) return true;
        if (sum < target) left++;
        else right--;
    }
    return false;
}
```

After sorting, each step discards a whole set of impossible pairs, so the scan is O(n). Sorting costs O(n log n), which dominates the total.

**Prefix sums** answer range-sum queries in O(1) after O(n) preprocessing:

```csharp
var prefix = new int[values.Length + 1];
for (var i = 0; i < values.Length; i++)
    prefix[i + 1] = prefix[i] + values[i];

// sum of values[from..to] inclusive
var rangeSum = prefix[to + 1] - prefix[from];
```

**In-place** algorithms modify the input using O(1) extra space. Reversing an array with two pointers is the canonical example: swap the ends and move inward.

When you solve an array problem, ask:

1. Is the input sorted, or can sorting help?
2. Do I need to preserve order?
3. Am I doing repeated work that a prefix structure or two pointers could remove?
4. What is the cost of the shifts I am triggering?
""",
    "workedExample": {
        "title": "Reverse in place",
        "markdown": r"""
Write a method that reverses `int[] values` using O(1) extra space.

```csharp
void Reverse(int[] values)
{
    var left = 0;
    var right = values.Length - 1;
    while (left < right)
    {
        (values[left], values[right]) = (values[right], values[left]);
        left++;
        right--;
    }
}
```
""",
        "steps": [
            "Two pointers start at the ends. They move toward each other; each iteration handles one pair.",
            "The tuple assignment swaps the two values without a temporary variable. It is evaluated before assignment, so it is safe.",
            "After each swap, both pointers have handled their positions, so increment `left` and decrement `right`.",
            "The loop stops when `left >= right`. The middle element of an odd-length array is already in place.",
            "Each element is visited at most once, so the time is O(n). Only the two indices are allocated, so extra space is O(1)."
        ]
    },
    "explainPrompt": "Why is indexing an array O(1) while inserting at the front is O(n)? Explain using the memory layout.",
    "checkQuestions": [
        q(
            "Why is `values[i]` O(1) for an array?",
            ["The runtime searches for the index from the beginning", "The address is computed from the base address and element size", "Arrays keep a dictionary of indices", "The JIT caches every index access"],
            1,
            "Contiguous elements and fixed size make address arithmetic constant time.",
            ["Scanning would be O(n).", "Correct.", "Arrays do not store an index dictionary.", "Caching is not what makes the access constant time."],
        ),
        q(
            "What is the cost of inserting a new element at index 0 of a `List<T>` with n elements?",
            ["O(1)", "O(log n)", "O(n)", "O(n²)"],
            2,
            "All existing elements must shift right by one position.",
            ["That would be appending when capacity is available, not front-insertion.", "List operations are not logarithmic.", "Correct.", "A single insertion is not quadratic."],
        ),
        q(
            "What advantage does a prefix-sum array provide?",
            ["It makes arbitrary range sums O(1) after O(n) preprocessing", "It makes insertion at the front O(1)", "It reduces memory usage to O(1)", "It sorts the array"],
            0,
            "Each range sum becomes a difference of two prefix values.",
            ["Correct.", "Prefix sums do not change insertion costs.", "The prefix array uses O(n) extra space.", "Prefix sums do not sort."],
        ),
    ],
    "dsa": {
        "topic": "arrays",
        "complexityQuestions": [
            complexity(
                "State the time and space complexity of this operation on an array of length n.",
                r"""void InsertAtBeginning(int[] values, int count, int newValue)
{
    for (var i = count; i > 0; i--)
        values[i] = values[i - 1];
    values[0] = newValue;
}""",
                "Time: O(n), Space: O(1)",
                "Every one of the n existing elements shifts one position to the right. The shifting is done in place, so extra space is constant (the array must have capacity for one more element).",
            ),
            complexity(
                "State the time and space complexity of building this prefix-sum array.",
                r"""int[] BuildPrefix(int[] values)
{
    var prefix = new int[values.Length + 1];
    for (var i = 0; i < values.Length; i++)
        prefix[i + 1] = prefix[i] + values[i];
    return prefix;
}""",
                "Time: O(n), Space: O(n)",
                "The loop runs once over n elements. The returned prefix array has n + 1 elements, which is O(n) extra space.",
            ),
        ],
        "practice": [
            practice(
                "Move zeroes",
                r"""
Given an integer array `values`, move every zero to the end while keeping the relative order of the non-zero elements. Do it in place with O(1) extra space.

```csharp
void MoveZeroes(int[] values)
```
""",
                [
                    "Think of a write pointer that marks where the next non-zero value belongs. A read pointer scans the array.",
                    "When the read pointer finds a non-zero value, write it at the write index and advance. After the scan, fill the rest with zeroes. Does this preserve relative order?",
                ],
                r"""void MoveZeroes(int[] values)
{
    var write = 0;
    for (var read = 0; read < values.Length; read++)
    {
        if (values[read] != 0)
        {
            values[write++] = values[read];
        }
    }
    while (write < values.Length)
        values[write++] = 0;
}""",
            ),
            practice(
                "Range sum queries",
                r"""
You are given an integer array and many queries of the form `(from, to)`. Each query asks for the sum of the elements between those inclusive indices. Design a structure that answers each query in O(1) after preprocessing.

```csharp
public sealed class RangeSummer
{
    public RangeSummer(int[] values) { }
    public int Sum(int from, int to) { }
}
```
""",
                [
                    "What information would let you get the sum of `values[0..to]` without adding each element again?",
                    "Store cumulative sums in an array of length n + 1. Then `sum(from..to) = prefix[to + 1] - prefix[from]`. Check the indices carefully.",
                ],
                r"""public sealed class RangeSummer
{
    private readonly int[] _prefix;

    public RangeSummer(int[] values)
    {
        _prefix = new int[values.Length + 1];
        for (var i = 0; i < values.Length; i++)
            _prefix[i + 1] = _prefix[i] + values[i];
    }

    public int Sum(int from, int to)
    {
        if (from < 0 || to >= _prefix.Length - 1 || from > to)
            throw new ArgumentOutOfRangeException();
        return _prefix[to + 1] - _prefix[from];
    }
}""",
            ),
        ],
    },
})

lessons.append({
    "id": "dsa-03-strings",
    "skill": "dsa",
    "topic": "strings",
    "order": 3,
    "title": "Strings: immutability, characters, and frequency",
    "summary": "Treat strings as sequences with real costs, then use frequency maps and two pointers safely.",
    "estimatedMinutes": 45,
    "theory": r"""
A C# `string` is an immutable sequence of UTF-16 code units. `Length` is the number of `char` values, not the number of user-perceived characters. Most Latin letters are one `char`; many emoji and some scripts require two `char`s (a surrogate pair), and grapheme clusters can be more complex still. For algorithm practice, assume ASCII unless the problem says otherwise, and be explicit when you rely on that assumption.

Because strings are immutable, every operation that appears to change one actually creates a new string:

```csharp
var result = "";
for (var i = 0; i < 10_000; i++)
    result += "x";
```

This loop is O(n²) in the total number of characters: each concatenation copies the accumulated string. Use `StringBuilder` for repeated appends:

```csharp
var builder = new StringBuilder();
for (var i = 0; i < 10_000; i++)
    builder.Append('x');
var result = builder.ToString(); // O(n) total
```

Indexing `text[i]` is O(1). Iterating all characters is O(n). `Substring(start, length)` creates a new string and copies O(length) characters in modern .NET. Comparing two strings can short-circuit on the first difference but is O(n) worst case.

Common string techniques:

1. **Frequency map.** Count characters, then compare counts. An array of 26 integers is cache-friendly for lowercase English; a `Dictionary<char,int>` handles arbitrary characters.
2. **Two pointers.** Palindromes, reversing, and comparing from both ends often use `left`/`right` indices.
3. **Sliding window.** Longest substring without repeats, fixed-size windows, and anagram search maintain a window with incremental counts.
4. **Normalization.** Case folding, trimming, and Unicode normalization change the data; decide early whether they are part of the problem.

```csharp
bool IsAnagram(string a, string b)
{
    if (a.Length != b.Length) return false;
    var counts = new int[26];
    foreach (var c in a) counts[c - 'a']++;
    foreach (var c in b)
        if (--counts[c - 'a'] < 0) return false;
    return true;
}
```

This is O(n) time and O(1) extra space for a fixed lowercase alphabet.

When solving string problems, clarify: Is the alphabet fixed? Is case sensitivity relevant? Are Unicode characters in scope? What is the expected output for empty strings? These details are part of the algorithm, not a footnote.
""",
    "workedExample": {
        "title": "Check a palindrome with two pointers",
        "markdown": r"""
Determine whether a string reads the same forwards and backwards, ignoring spaces, punctuation, and case.

```csharp
bool IsPalindrome(string text)
{
    var left = 0;
    var right = text.Length - 1;
    while (left < right)
    {
        while (left < right && !char.IsLetterOrDigit(text[left])) left++;
        while (left < right && !char.IsLetterOrDigit(text[right])) right--;
        if (char.ToLowerInvariant(text[left]) != char.ToLowerInvariant(text[right]))
            return false;
        left++;
        right--;
    }
    return true;
}
```
""",
        "steps": [
            "Two indices begin at the ends. Each loop skips characters that are not letters or digits, because the problem says to ignore them.",
            "After skipping, the characters at `left` and `right` are the next meaningful pair. Compare them case-insensitively.",
            "A mismatch proves the string is not a palindrome, so return `false` immediately.",
            "If they match, move both pointers inward. The process continues until the pointers meet or cross.",
            "Each character is visited at most once, so the time is O(n). Only two integer indices are used, so extra space is O(1)."
        ]
    },
    "explainPrompt": "Why is repeatedly concatenating strings in a loop O(n²), and how does StringBuilder avoid it?",
    "checkQuestions": [
        q(
            "What is the time complexity of building a string with `+=` in a loop for n total characters?",
            ["O(1)", "O(n)", "O(n²)", "O(log n)"],
            2,
            "Each concatenation copies the current accumulated string, so the total copied characters form 1 + 2 + ... + n.",
            ["Each operation allocates and copies.", "Only the final length is n; the copying is cumulative.", "Correct.", "The work grows quadratically, not logarithmically."],
        ),
        q(
            "Why is a frequency array of size 26 useful for lowercase English letters?",
            ["It sorts the string", "It gives O(1) count updates and cache-friendly memory", "It supports Unicode automatically", "It makes strings mutable"],
            1,
            "A fixed-size array maps each letter to a slot, making counts O(1) with no hashing overhead.",
            ["Counting does not sort.", "Correct.", "A 26-slot array does not cover arbitrary Unicode.", "Strings remain immutable."],
        ),
        q(
            "What does `text.Length` represent in C#?",
            ["The number of Unicode grapheme clusters", "The number of UTF-16 code units", "The number of bytes in memory", "The number of words"],
            1,
            "`string.Length` counts UTF-16 `char` values, which may differ from user-perceived characters.",
            ["Grapheme clusters can span multiple chars.", "Correct.", "UTF-16 uses two bytes per code unit, so bytes are not the same as Length.", "Words are not counted by Length."],
        ),
    ],
    "dsa": {
        "topic": "strings",
        "complexityQuestions": [
            complexity(
                "State the time and space complexity of this method for a string of length n.",
                r"""string BuildStars(int n)
{
    var result = "";
    for (var i = 0; i < n; i++)
        result += "*";
    return result;
}""",
                "Time: O(n²), Space: O(n)",
                "Each `+=` creates a new string and copies the previous characters, giving 1 + 2 + ... + n copies. The final string uses O(n) space, and old intermediate strings are garbage.",
            ),
            complexity(
                "State the time complexity of this anagram check for strings of length n over a fixed lowercase alphabet.",
                r"""bool IsAnagram(string a, string b)
{
    if (a.Length != b.Length) return false;
    var counts = new int[26];
    foreach (var c in a) counts[c - 'a']++;
    foreach (var c in b)
        if (--counts[c - 'a'] < 0) return false;
    return true;
}""",
                "Time: O(n), Space: O(1)",
                "Each string is scanned once. The count array has a fixed 26 slots, so extra space is constant with respect to n.",
            ),
        ],
        "practice": [
            practice(
                "Valid anagram",
                r"""
Given two strings `a` and `b`, return `true` if one is an anagram of the other. An anagram uses every character exactly the same number of times.

```csharp
bool IsAnagram(string a, string b)
```

Assume lowercase English letters for this exercise. State the complexity of your solution.
""",
                [
                    "If the lengths differ, what can you conclude immediately?",
                    "Use a count array indexed by `c - 'a'`. Increment for the first string, decrement for the second, and fail if any count goes negative.",
                ],
                r"""bool IsAnagram(string a, string b)
{
    if (a.Length != b.Length) return false;

    var counts = new int[26];
    foreach (var c in a) counts[c - 'a']++;
    foreach (var c in b)
    {
        if (--counts[c - 'a'] < 0) return false;
    }
    return true;
}

// Time: O(n), Space: O(1) for the fixed 26-letter alphabet.""",
            ),
            practice(
                "Longest common prefix",
                r"""
Given an array of strings, return the longest common prefix shared by all of them. Return an empty string if there is none.

```csharp
string LongestCommonPrefix(string[] words)
```
""",
                [
                    "Start with the first word as the candidate prefix. Compare it with each subsequent word and shorten it until it matches.",
                    "A simple approach compares character by character at each position across all words. Stop at the first mismatch or when a word ends. What is the worst-case complexity?",
                ],
                r"""string LongestCommonPrefix(string[] words)
{
    if (words.Length == 0) return "";

    var prefix = words[0];
    for (var i = 1; i < words.Length; i++)
    {
        while (!words[i].StartsWith(prefix, StringComparison.Ordinal))
        {
            prefix = prefix[..^1];
            if (prefix.Length == 0) return "";
        }
    }
    return prefix;
}

// Worst case O(n * L), where L is the length of the shortest word.
// Each shortening copies the prefix, so a character-by-character scan can be clearer in performance-critical code.""",
            ),
        ],
    },
})

for item in lessons:
    path = ROOT / f"{item['order']:02d}-{item['id']}.json"
    path.write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding="utf-8")
    print(path.name)

print(f"Wrote {len(lessons)} DSA lessons to {ROOT}")
