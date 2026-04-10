using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Buat via: klik kanan di Project → Quest → New Quest
///
/// Setiap QuestData = 1 quest dengan beberapa step.
/// Sambungkan quest berikutnya lewat field "nextQuest".
///
/// Contoh rantai:
///   Quest_Intro → Quest_FindSword → Quest_KillBoss → (kosong = tamat)
/// </summary>
[CreateAssetMenu(fileName = "Quest_New", menuName = "Quest/New Quest")]
public class QuestData : ScriptableObject
{
    [System.Serializable]
    public class Step
    {
        [Tooltip("ID unik step ini. Harus sama dengan Step ID di QuestTrigger.")]
        public string stepId;

        [TextArea(2, 3)]
        [Tooltip("Kalimat objective yang tampil di HUD pemain.")]
        public string objective;
    }

    [Header("Identitas Quest")]
    [Tooltip("ID unik quest. Contoh: 'q_intro', 'q_cari_pedang', 'q_bunuh_bos'")]
    public string questId;

    [Tooltip("Judul singkat yang tampil di header HUD. Contoh: 'MISI: Temui Ketua'")]
    public string questTitle;

    [Header("Daftar Step")]
    public List<Step> steps = new();

    [Header("Quest Berikutnya")]
    [Tooltip("Quest yang langsung aktif setelah quest ini selesai. Kosongkan = tidak ada lanjutan.")]
    public QuestData nextQuest;
}
