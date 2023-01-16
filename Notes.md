# Flattening sequences with `SelectMany()`

Ex:
```csharp
List<List<int>> lists = new List<List<int>>
{
    new List<int> { 1, 2, 3 },
    new List<int> { 4, 5, 6 },
    new List<int> { 7, 8, 9 }
};

List<int> flattenedList = lists.SelectMany(list => list).ToList();
// flattenedList = { 1, 2, 3, 4, 5, 6, 7, 8, 9 }
```