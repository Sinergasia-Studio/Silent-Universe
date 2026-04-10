using UnityEngine;

/// <summary>
/// DialogueData — ScriptableObject
/// Buat via: klik kanan di Project → Create → Dialogue → Dialogue Data
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [TextArea(2, 4)]
    public string npcName;

    [Tooltip("Centang jika ini dialogue narator — DialogueUI NPC akan diabaikan")]
    public bool isNarrator;

    public DialogueNode[] nodes;
}

[System.Serializable]
public class DialogueNode
{
    public string nodeID;           // unik, misal "start", "node_1", "end"

    [TextArea(2, 5)]
    public string npcText;          // teks yang diucapkan NPC

    public DialogueChoice[] choices; // kalau kosong = akhir dialogue
}

[System.Serializable]
public class DialogueChoice
{
    public string choiceText;       // teks pilihan player
    public string nextNodeID;       // nodeID tujuan setelah pilih ini
}