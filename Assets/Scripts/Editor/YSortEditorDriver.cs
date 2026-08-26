using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Faz o Y-Sort valer na Scene View sem entrar em Play.
//
// Em Play quem toca o sistema e um objeto escondido criado pelo proprio YSortWorld. Fora de
// Play nao existe objeto nenhum para chamar LateUpdate, entao o editor chama daqui.
//
// Isto e SO conveniencia de autoria: com o preview desligado a Scene View mostra os Sorting
// Orders antigos, e o jogo continua ordenando certo em Play do mesmo jeito. O interruptor
// esta em Assets/Resources/YSortSettings.asset > Editor > Preview In Editor.
[InitializeOnLoad]
public static class YSortEditorDriver
{
    static YSortEditorDriver()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        // Trocar de cena, ou sair do Prefab Mode, deixa para tras entradas que apontam para
        // objetos de outra cena. Remontar do zero e mais barato que tentar consertar.
        EditorSceneManager.sceneOpened += (_, __) => YSortWorld.Clear();
        PrefabStage.prefabStageOpened += _ => YSortWorld.Clear();
        PrefabStage.prefabStageClosing += _ => YSortWorld.Clear();
    }

    private static void Tick()
    {
        // Em Play o driver de verdade ja esta rodando; dois donos escrevendo no mesmo
        // Sorting Order so serviria para brigar.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Dentro do Prefab Mode a cena de preview tem UM objeto no meio do nada. Ordenar ali
        // nao diz nada sobre o jogo e ainda escreveria no arquivo do prefab.
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;

        if (!YSortSettings.PreviewInEditor) return;

        // Mexer em sortingOrder por script nao faz a Scene View se redesenhar sozinha: os
        // numeros ja estao certos, mas a imagem na tela continua a antiga ate voce clicar em
        // alguma coisa. Era por isso que o Play mostrava a ordem certa e a cena nao.
        if (YSortWorld.Pump((float)EditorApplication.timeSinceStartup))
            SceneView.RepaintAll();
    }
}
