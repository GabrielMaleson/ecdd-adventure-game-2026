using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Y-Sort > ...
//
// Nao ha nada aqui que PRECISE ser rodado: o YSortWorld ordena a cena sozinho, sem
// componente e sem migracao. Estes menus sao diagnostico e ajuste fino.
//
// O relatorio e o que vale a pena: ele nao escreve nada, mostra quantos objetos o sistema
// esta vendo, em que Sorting Layers eles estao hoje, e se o Precision cabe em 16 bits para
// o tamanho real do mapa.
public static class YSortTools
{
    [MenuItem("Tools/Y-Sort/Relatorio (nao muda nada)", priority = 0)]
    private static void Report()
    {
        List<GameObject> targets = FindRoots(out int skipped);

        var byLayer = new Dictionary<string, int>();
        var orphanLayers = new HashSet<int>();
        int tuned = 0, excluded = 0;

        foreach (GameObject go in targets)
        {
            YSort t = go.GetComponent<YSort>();
            if (t != null)
            {
                if (t.naoOrdenar) excluded++;
                else tuned++;
            }

            foreach (SpriteRenderer r in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (YSortWorld.ShouldSkip(r)) continue;

                string name = SortingLayer.IDToName(r.sortingLayerID);

                if (string.IsNullOrEmpty(name))
                {
                    orphanLayers.Add(r.sortingLayerID);
                    name = $"<inexistente {r.sortingLayerID}>";
                }

                byLayer.TryGetValue(name, out int n);
                byLayer[name] = n + 1;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Objetos ordenados por Y: {targets.Count}");
        sb.AppendLine($"   com YSort para ajuste de ancora: {tuned}");
        sb.AppendLine($"   marcados como 'nao ordenar': {excluded}");
        sb.AppendLine($"Deixados INTACTOS (sem collider solido = chao, mais Dialogue/Canvas/nevoa): {skipped}");
        sb.AppendLine();
        sb.AppendLine("Sprites por Sorting Layer hoje:");
        foreach (var kv in byLayer) sb.AppendLine($"   {kv.Key}: {kv.Value}");

        YSortSettings s = YSortSettings.Current;
        sb.AppendLine();
        if (s == null)
        {
            sb.AppendLine("SEM Assets/Resources/YSortSettings.asset — crie por " +
                          "Create > ECDD > Y-Sort Settings, senao o padrao 100 e usado.");
        }
        else
        {
            sb.AppendLine($"Todos vao para a camada: {s.sortingLayerName}" +
                          (s.forceSortingLayer ? "" : "  (DESLIGADO — ninguem e movido)"));
            sb.AppendLine($"Precision: {s.precision}   " +
                          $"Preview no editor: {(s.previewInEditor ? "ligado" : "desligado")}   " +
                          $"Varredura: {(s.rescanInterval > 0f ? s.rescanInterval + "s" : "so ao carregar")}");
        }

        if (orphanLayers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"ATENCAO: {orphanLayers.Count} Sorting Layer(s) que nao existem mais " +
                          "neste projeto (vieram junto com o pacote de arte). Os sprites nelas " +
                          "desenham em ordem imprevisivel. Como tudo e movido para uma camada " +
                          "so, isso ja fica resolvido.");
        }

        // Alcance real do mapa, que e o que decide se o Precision cabe em 16 bits.
        if (targets.Count > 0)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (GameObject go in targets)
            {
                float y = go.transform.position.y + YSortWorld.AnchorOffsetFor(go.transform, go.GetComponent<YSort>());
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            int p = s != null ? s.precision : 100;
            sb.AppendLine();
            sb.AppendLine($"Y do mapa: {minY:F1} a {maxY:F1}  ->  Sorting Order de " +
                          $"{Mathf.RoundToInt(-maxY * p)} a {Mathf.RoundToInt(-minY * p)}");
            sb.AppendLine(Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY)) * p > 32000
                ? "   NAO CABE em 16 bits. Baixe o Precision."
                : "   Cabe com folga.");
        }

        Debug.Log("=== Y-SORT — RELATORIO ===\n" + sb);
    }

    // Atalho para o caso 1 do YSort: a linha do chao esta errada e voce quer mexer nela.
    // Adiciona o componente e ja deixa selecionado para arrastar o Deslocamento.
    [MenuItem("Tools/Y-Sort/Ajustar ancora da selecao", priority = 20)]
    private static void TuneSelection()
    {
        int n = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            GameObject root = YSortWorld.RootOf(go.transform).gameObject;

            if (root.GetComponent<YSort>() == null)
            {
                Undo.AddComponent<YSort>(root);
                n++;
            }
        }

        YSortWorld.Clear();
        Debug.Log($"Y-Sort: {n} objeto(s) ganharam YSort. Selecione e mexa no Deslocamento — " +
                  "o gizmo amarelo mostra onde o sistema acha que esta o chao.");
    }

    [MenuItem("Tools/Y-Sort/Remontar agora", priority = 21)]
    private static void Rebuild()
    {
        YSortWorld.Clear();
        YSortWorld.Rescan();
        YSortWorld.Apply();
        Debug.Log($"Y-Sort: {YSortWorld.Count} objetos remontados.");
    }

    // Mesma definicao de "objeto" que o motor usa, para relatorio e motor nunca divergirem.
    private static List<GameObject> FindRoots(out int skipped)
    {
        var roots = new HashSet<GameObject>();
        var rejected = new HashSet<GameObject>();
        skipped = 0;

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (YSortWorld.ShouldSkip(r)) { skipped++; continue; }

            Transform root = YSortWorld.RootOf(r.transform);

            // Sem collider solido o objeto e chao (estrada, poca, grama, interior) e fica
            // INTACTO. Contar separado, porque "quantos ficaram de fora" e justamente o
            // numero que denuncia a regra errada.
            if (!YSortWorld.Qualifies(root, root.GetComponent<YSort>()))
            {
                rejected.Add(root.gameObject);
                continue;
            }

            roots.Add(root.gameObject);
        }

        skipped += rejected.Count;

        var list = new List<GameObject>(roots);
        list.Sort((a, b) => a.name.CompareTo(b.name));
        return list;
    }
}
