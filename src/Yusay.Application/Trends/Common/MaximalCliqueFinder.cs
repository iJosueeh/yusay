namespace Yusay.Application.Trends.Common;

/// <summary>
/// Algoritmo de Bron-Kerbosch con pivote para encontrar todos los cliques maximales
/// en un grafo no dirigido de versiones, conforme a las reglas de DR-DOM-003.
/// </summary>
public static class MaximalCliqueFinder
{
    public static List<List<T>> FindMaximalCliques<T>(
        IReadOnlyList<T> vertices,
        Func<T, Guid> idSelector,
        HashSet<(Guid, Guid)> undirectedEdges)
    {
        var cliques = new List<List<T>>();
        if (vertices.Count == 0)
        {
            return cliques;
        }

        var vertexMap = vertices.ToDictionary(idSelector);
        var allIds = vertices.Select(idSelector).ToList();

        bool IsAdjacent(Guid u, Guid v) =>
            u != v && undirectedEdges.Contains((u, v));

        void BronKerbosch(HashSet<Guid> r, HashSet<Guid> p, HashSet<Guid> x)
        {
            if (p.Count == 0 && x.Count == 0)
            {
                cliques.Add(r.Select(id => vertexMap[id]).ToList());
                return;
            }

            // Seleccionar pivote en P ∪ X que maximice |P ∩ N(u)|
            Guid pivot = Guid.Empty;
            int maxIntersections = -1;

            foreach (var u in p.Concat(x))
            {
                int count = 0;
                foreach (var v in p)
                {
                    if (IsAdjacent(u, v))
                    {
                        count++;
                    }
                }

                if (count > maxIntersections)
                {
                    maxIntersections = count;
                    pivot = u;
                }
            }

            // Candidatos = P \ N(pivote)
            var candidates = p.Where(v => pivot == Guid.Empty || !IsAdjacent(pivot, v)).ToList();

            foreach (var v in candidates)
            {
                var neighbors = allIds.Where(nbr => IsAdjacent(v, nbr)).ToHashSet();

                var newR = new HashSet<Guid>(r) { v };
                var newP = new HashSet<Guid>(p);
                newP.IntersectWith(neighbors);
                var newX = new HashSet<Guid>(x);
                newX.IntersectWith(neighbors);

                BronKerbosch(newR, newP, newX);

                p.Remove(v);
                x.Add(v);
            }
        }

        BronKerbosch(new HashSet<Guid>(), new HashSet<Guid>(allIds), new HashSet<Guid>());
        return cliques;
    }
}
