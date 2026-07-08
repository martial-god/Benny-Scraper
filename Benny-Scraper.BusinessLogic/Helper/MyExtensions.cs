namespace BennyScraper.BusinessLogic.Helper;

public static class MyExtensions
{
    /// <summary>
    /// Extension method for ICollection to add a range of items. Make
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="items"></param>
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
    public static int FindIndex<T>(this IList<T> list, Predicate<T> match)
    {
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
    public static void ReverseInPlace<T>(this IList<T> list)
    {
        var count = list.Count;
        for (var i = 0; i < count / 2; i++)
        {
            (list[i], list[count - 1 - i]) = (list[count - 1 - i], list[i]);
        }
    }
}