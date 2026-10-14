namespace PersonalAssistant.Actions;

/// <summary>
/// Compara nomes de forma tolerante a pequenos erros — sobretudo os do reconhecimento de voz,
/// que escreve "Esther" quando disseste "Ester", ou "Ana" como "Anna".
///
/// Usa a "distância de Levenshtein": o número mínimo de letras a inserir, apagar ou trocar
/// para transformar uma palavra na outra ("ester" → "esther" = 1, só inserir o "h").
/// </summary>
public static class NameMatcher
{
    /// <summary>
    /// Quantos erros aceitamos, conforme o tamanho do nome. Nomes curtos têm de ser exatos
    /// ("Ana" e "Ema" são pessoas diferentes!); nomes maiores aceitam 1 ou 2 letras de diferença.
    /// </summary>
    public static int MaxDistanceFor(string normalizedName) => normalizedName.Length switch
    {
        <= 3 => 0,
        <= 6 => 1,
        _ => 2
    };

    /// <summary>true se <paramref name="candidate"/> é (quase) igual a <paramref name="name"/>.</summary>
    public static bool IsMatch(string candidate, string name)
    {
        var a = TextNormalizer.Normalize(candidate);
        var b = TextNormalizer.Normalize(name);
        return Distance(a, b) <= MaxDistanceFor(b);
    }

    /// <summary>Distância de Levenshtein entre duas palavras (algoritmo clássico com uma tabela).</summary>
    public static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
