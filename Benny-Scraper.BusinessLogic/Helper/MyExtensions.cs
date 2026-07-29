namespace BennyScraper.BusinessLogic.Helper;

public static class MyExtensions
{
    /// <summary>
    /// Extension method for <see cref="ICollection{T}"/> to add a range of items. No-op if either <paramref name="collection"/> or <paramref name="items"/> is null.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to add the items to.</param>
    /// <param name="items">The items to add to the collection.</param>
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        if (collection == null || items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    /// <summary>
    /// Clears an existing collection and repopulates it from another sequence, in place.
    /// Used to satisfy CA2227 (no public collection setter) while still supporting wholesale replacement.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to clear and repopulate.</param>
    /// <param name="items">The items used to repopulate the collection.</param>
    public static void ReplaceWith<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        if (collection == null || items == null)
        {
            return;
        }

        collection.Clear();
        collection.AddRange(items);
    }

    /// <summary>
    /// Equivalent to <see cref="List{T}.FindIndex(Predicate{T})"/>, for the <see cref="IList{T}"/> types
    /// exposed instead of concrete <see cref="List{T}"/> (CA1002).
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to search.</param>
    /// <param name="match">The predicate used to test each element.</param>
    /// <returns>The zero-based index of the first element that matches the predicate, or -1 if no match is found.</returns>
    public static int FindIndex<T>(this IList<T> list, Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(match);

        for (var i = 0; i < list.Count; i++)
        {
            if (match(list[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Reverses an <see cref="IList{T}"/> in place. Named distinctly from LINQ's <see cref="Enumerable.Reverse{T}"/>
    /// (which returns a new sequence) to avoid overload-resolution ambiguity at call sites.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to reverse in place.</param>
    public static void ReverseInPlace<T>(this IList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        var count = list.Count;
        for (var i = 0; i < count / 2; i++)
        {
            (list[i], list[count - 1 - i]) = (list[count - 1 - i], list[i]);
        }
    }
}