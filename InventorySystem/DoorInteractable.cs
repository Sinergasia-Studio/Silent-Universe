using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DoorInteractable — rotasi pintu otomatis berpusat di sisi kanan atau kiri mesh.
/// Tidak perlu atur pivot manual.
///
/// Setup:
///   1. Pasang script ini pada GameObject pintu (yang punya MeshRenderer/Renderer)
///   2. Pasang Collider pada GameObject yang sama atau child
///   3. Pilih Pivot Side: Right atau Left di Inspector
///   4. Jika terkunci, assign Required Key
///   5. Isi Save Key dengan ID unik per pintu (misal "Door_Warehouse_01")
///      agar state buka/tutup/kunci tersimpan di save file.
///
/// State yang disimpan (via WorldFlags):
///   - Apakah pintu terbuka   → "Door_<saveKey>_o"
///   - Apakah pintu terkunci  → "Door_<saveKey>_k"
///   - Apakah key sudah dipakai (consumed) → "Door_<saveKey>_u"  (hanya jika consumeKey = true)
///
/// Optimasi:
///   - Save key di-cache saat Awake, tidak ada string concatenation saat runtime
///   - LoadSavedState melakukan satu parse WorldFlags (via SaveFile.Data langsung) alih-alih 5x
///   - Unlock + Open dalam satu interaksi hanya menulis disk 1x (FlushSaveState)
///   - Tidak ada ForceWrite ganda dalam satu frame
/// </summary>
public class DoorInteractable : MonoBehaviour, IInteractable
{
    public enum PivotSide { Right, Left }

    [Header("Interact Settings")]
    [SerializeField] private string promptTextClosed = "Tahan [E] untuk buka";
    [SerializeField] private string promptTextOpen   = "Tahan [E] untuk tutup";
    [SerializeField] private string promptTextLocked = "[Terkunci]";

    [Header("Lock & Key")]
    [Tooltip("Kosongkan jika pintu tidak perlu key")]
    [SerializeField] private KeyItem requiredKey;
    [Tooltip("Hapus key dari inventory setelah dipakai?")]
    [SerializeField] private bool consumeKey = false;

    [Header("Door State")]
    [SerializeField] private bool startLocked = false;
    [SerializeField] private bool startOpen   = false;

    [Header("Rotation")]
    [Tooltip("Right = engsel di sisi +X lokal, Left = engsel di sisi -X lokal")]
    [SerializeField] private PivotSide pivotSide = PivotSide.Right;
    [Tooltip("Sudut buka pintu dalam derajat. Negatif = buka ke arah sebaliknya.")]
    [SerializeField] private float openAngle    = 90f;
    [SerializeField] private float animDuration = 0.5f;
    [SerializeField] private AnimationCurve animCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Save Settings")]
    [Tooltip("ID unik pintu ini untuk disimpan ke save file. WAJIB diisi agar state tersimpan.\n" +
             "Contoh: 'Door_Warehouse_01', 'Door_House_Front'.\n" +
             "Jangan ada dua pintu dengan SaveKey yang sama di scene yang sama.\n" +
             "Kosongkan jika tidak perlu di-save.")]
    [SerializeField] private string saveKey = "";

    [Header("Events")]
    public UnityEvent onDoorOpened;
    public UnityEvent onDoorClosed;
    public UnityEvent onDoorLocked;
    public UnityEvent onDoorUnlocked;
    public UnityEvent onWrongKey;
    public UnityEvent onInteractLocked;

    // ── Cached save keys (di-build sekali saat Awake, zero allocation saat runtime) ──
    // Suffix pendek sengaja dipilih agar total key string pendek dan cepat di-parse
    // WorldFlags. Makin banyak pintu, makin terasa benefitnya.
    private string _keyOpen;   // "Door_<saveKey>_o"
    private string _keyLock;   // "Door_<saveKey>_k"
    private string _keyUsed;   // "Door_<saveKey>_u"
    private bool   _hasSaveKey;

    // ── state ──
    private bool       _isOpen;
    private bool       _isLocked;
    private bool       _isAnimating;

    // Simpan pivot dalam Local Space parent agar pintu mengikuti parent yang bergerak (lift, dll.)
    private Transform  _pivotParent;
    private Vector3    _pivotLocal;
    private Quaternion _closedRot;
    private Quaternion _openRot;

    public bool IsOpen   => _isOpen;
    public bool IsLocked => _isLocked;

    // ── IInteractable ──
    public bool CanInteract => !_isAnimating;
    public string PromptText
    {
        get
        {
            if (_isLocked) return promptTextLocked;
            return _isOpen ? promptTextOpen : promptTextClosed;
        }
    }

    // ── lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache save keys sekali — tidak ada concatenation lagi setelah ini
        _hasSaveKey = !string.IsNullOrEmpty(saveKey);
        if (_hasSaveKey)
        {
            _keyOpen = "Door_" + saveKey + "_o";
            _keyLock = "Door_" + saveKey + "_k";
            _keyUsed = "Door_" + saveKey + "_u";
        }

        // Hitung pivot
        _pivotParent = transform.parent;
        Vector3 pivotWorld = GetEdgePivot();
        _pivotLocal  = _pivotParent != null
            ? _pivotParent.InverseTransformPoint(pivotWorld)
            : pivotWorld;

        _closedRot = transform.rotation;
        _openRot   = Quaternion.AngleAxis(openAngle, Vector3.up) * _closedRot;

        if (_hasSaveKey)
            LoadSavedState();
        else
            ApplyDefaultState();
    }

    // ── Save / Load ────────────────────────────────────────────────────────────

    /// <summary>
    /// Baca tiga flag sekaligus dari raw worldFlags string — satu parse saja.
    /// Menghindari 5x Dictionary allocation yang terjadi jika pakai WorldFlags.Get/Has berulang.
    /// </summary>
    private void LoadSavedState()
    {
        // Baca langsung dari string mentah, bukan via WorldFlags.Get() agar tidak
        // parse berulang. Kita hanya perlu tiga key — cukup scan satu kali.
        bool foundOpen = false, foundLock = false, foundUsed = false;
        bool savedOpen = false, savedLock = false, savedUsed = false;

        string raw = SaveFile.Data.worldFlags ?? "";
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var entry in raw.Split('|'))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                int eq = entry.IndexOf('=');
                if (eq < 0) continue;

                string k = entry.Substring(0, eq);
                bool   v = entry.Length > eq + 1 && entry[eq + 1] == '1';

                if      (k == _keyOpen) { foundOpen = true; savedOpen = v; }
                else if (k == _keyLock) { foundLock = true; savedLock = v; }
                else if (k == _keyUsed) { foundUsed = true; savedUsed = v; }
            }
        }

        _isLocked = foundLock ? savedLock : startLocked;

        bool shouldBeOpen = foundOpen ? savedOpen : startOpen;
        if (shouldBeOpen)
        {
            _isOpen = true;
            RotateAroundPivot(_openRot);
        }

        // Jika key sudah dipakai sebelumnya tapi pintu entah bagaimana masih locked
        // (edge case: save korup / race condition), paksa unlock tanpa nulis ulang ke disk.
        if (foundUsed && savedUsed && _isLocked)
        {
            _isLocked = false;
            Debug.LogWarning($"[Door] '{saveKey}' — key sudah dipakai tapi pintu masih locked di save. Paksa unlock.");
        }

        Debug.Log($"[Door] Restore '{saveKey}' — Open:{_isOpen} Locked:{_isLocked} KeyUsed:{savedUsed}");
    }

    private void ApplyDefaultState()
    {
        _isLocked = startLocked;
        if (startOpen)
        {
            _isOpen = true;
            RotateAroundPivot(_openRot);
        }
    }

    /// <summary>
    /// Tulis semua state pintu ke WorldFlags sekaligus lalu satu kali ForceWrite.
    /// Menghindari 2–3 ForceWrite terpisah yang terjadi ketika unlock + open dilakukan
    /// dalam satu frame (misal: player pakai key → pintu dibuka).
    /// </summary>
    private void FlushSaveState(bool includeKeyUsed = false)
    {
        if (!_hasSaveKey) return;

        // Tulis langsung ke SaveFile.Data tanpa ForceWrite — kita kumpulkan dulu
        WorldFlags.SetNoWrite(_keyOpen, _isOpen);
        WorldFlags.SetNoWrite(_keyLock, _isLocked);
        if (includeKeyUsed)
            WorldFlags.SetNoWrite(_keyUsed, true);

        // Satu kali tulis ke disk
        SaveFile.ForceWrite();

        Debug.Log($"[Door] Flush '{saveKey}' — Open:{_isOpen} Locked:{_isLocked}" +
                  (includeKeyUsed ? " KeyUsed:true" : ""));
    }

    // ── IInteractable ──────────────────────────────────────────────────────────

    public void OnInteract(GameObject interactor)
    {
        if (_isAnimating) return;

        if (_isLocked)
        {
            onInteractLocked.Invoke();

            var inventory = PlayerInventory.Instance;
            if (requiredKey != null && inventory != null && inventory.HasKey(requiredKey))
            {
                // Ubah state dulu semua, baru flush sekali di akhir
                _isLocked = false;
                onDoorUnlocked.Invoke();

                bool keyConsumed = false;
                if (consumeKey)
                {
                    inventory.RemoveKey(requiredKey);
                    keyConsumed = true;
                }

                // Langsung open setelah unlock (tanpa call Toggle() yang akan trigger
                // save terpisah)
                StartCoroutine(AnimateDoor(_openRot));
                _isOpen = true;
                onDoorOpened.Invoke();

                // Satu kali write untuk unlock + open + keyused
                FlushSaveState(includeKeyUsed: keyConsumed);
            }
            else
            {
                onWrongKey.Invoke();
                Debug.Log($"[Door] Butuh key: {(requiredKey != null ? requiredKey.keyName : "?")}");
            }
            return;
        }

        Toggle();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Toggle()  { if (_isAnimating) return; if (_isOpen) Close(); else Open(); }

    public void Open()
    {
        if (_isOpen || _isAnimating) return;
        StartCoroutine(AnimateDoor(_openRot));
        _isOpen = true;
        onDoorOpened.Invoke();
        FlushSaveState();
    }

    public void Close()
    {
        if (!_isOpen || _isAnimating) return;
        StartCoroutine(AnimateDoor(_closedRot));
        _isOpen = false;
        onDoorClosed.Invoke();
        FlushSaveState();
    }

    public void Lock()
    {
        _isLocked = true;
        onDoorLocked.Invoke();
        FlushSaveState();
    }

    public void Unlock()
    {
        _isLocked = false;
        onDoorUnlocked.Invoke();
        FlushSaveState();
    }

    public void SetLocked(bool locked) { if (locked) Lock(); else Unlock(); }

    /// <summary>
    /// Reset pintu ke state awal Inspector dan hapus semua flag save.
    /// Berguna untuk DEV / quest reset.
    /// </summary>
    public void ResetToDefault()
    {
        if (_hasSaveKey)
        {
            // Hapus tiga key sekaligus — Remove() masing-masing ForceWrite,
            // tapi ini hanya dipanggil dari DEV/reset jadi overhead tidak masalah.
            WorldFlags.Remove(_keyOpen);
            WorldFlags.Remove(_keyLock);
            WorldFlags.Remove(_keyUsed);
        }

        _isLocked    = startLocked;
        _isAnimating = false;

        if (startOpen && !_isOpen)
        {
            _isOpen = true;
            RotateAroundPivot(_openRot);
        }
        else if (!startOpen && _isOpen)
        {
            _isOpen = false;
            RotateAroundPivot(_closedRot);
        }
    }

    // ── Pivot ──────────────────────────────────────────────────────────────────

    private Vector3 GetPivotWorld()
    {
        if (_pivotParent != null)
            return _pivotParent.TransformPoint(_pivotLocal);
        return _pivotLocal;
    }

    private Vector3 GetEdgePivot()
    {
        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Bounds localBounds = GetLocalBounds(r);
            float localX = (pivotSide == PivotSide.Right) ? localBounds.max.x : localBounds.min.x;
            Vector3 localPivot = new Vector3(localX, localBounds.center.y, localBounds.center.z);
            return transform.TransformPoint(localPivot);
        }

        Debug.LogWarning("[Door] Renderer tidak ditemukan, pivot fallback ke transform.position", this);
        return transform.position;
    }

    private Bounds GetLocalBounds(Renderer r)
    {
        if (r is MeshRenderer mr)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3[] verts = mf.sharedMesh.vertices;
                Matrix4x4 mtx  = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;

                Vector3 min = Vector3.positiveInfinity;
                Vector3 max = Vector3.negativeInfinity;
                foreach (Vector3 v in verts)
                {
                    Vector3 p = mtx.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                return new Bounds((min + max) * 0.5f, max - min);
            }
        }

        Vector3 localCenter = transform.InverseTransformPoint(r.bounds.center);
        Vector3 localSize   = r.bounds.size;
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        return new Bounds(localCenter, localSize);
    }

    private void RotateAroundPivot(Quaternion targetWorldRot)
    {
        Vector3    pivotWorld = GetPivotWorld();
        Quaternion delta      = targetWorldRot * Quaternion.Inverse(transform.rotation);
        transform.rotation    = targetWorldRot;
        transform.position    = pivotWorld + delta * (transform.position - pivotWorld);
    }

    // ── Animation ──────────────────────────────────────────────────────────────

    private IEnumerator AnimateDoor(Quaternion targetRot)
    {
        _isAnimating = true;
        Quaternion startRot = transform.rotation;
        Vector3    startPos = transform.position;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = animCurve.Evaluate(Mathf.Clamp01(elapsed / animDuration));

            Quaternion current = Quaternion.Lerp(startRot, targetRot, t);
            Quaternion delta   = current * Quaternion.Inverse(startRot);

            Vector3 pivotWorld    = GetPivotWorld();
            transform.rotation    = current;
            transform.position    = pivotWorld + delta * (startPos - pivotWorld);

            yield return null;
        }

        // snap ke target
        Vector3    finalPivot = GetPivotWorld();
        Quaternion finalDelta = targetRot * Quaternion.Inverse(startRot);
        transform.rotation = targetRot;
        transform.position = finalPivot + finalDelta * (startPos - finalPivot);

        _isAnimating = false;
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 pivotWorld = Application.isPlaying ? GetPivotWorld() : GetEdgePivot();
        Vector3 axisWorld  = Vector3.up;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivotWorld, 0.05f);

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(pivotWorld + Vector3.up * 0.3f, $"  Pivot: {pivotSide}");

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pivotWorld - axisWorld * 0.2f, pivotWorld + axisWorld * 1f);
        Gizmos.DrawSphere(pivotWorld + axisWorld * 1f, 0.03f);

        Quaternion closedQ   = Application.isPlaying ? _closedRot : transform.rotation;
        Vector3    closedDir = closedQ * Vector3.forward;
        float      arcRadius = 0.7f;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pivotWorld, pivotWorld + closedDir * arcRadius);

        Quaternion openQ   = Quaternion.AngleAxis(openAngle, Vector3.up) * closedQ;
        Vector3    openDir = openQ * Vector3.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(pivotWorld, pivotWorld + openDir * arcRadius);

        UnityEditor.Handles.color = new Color(1f, 0.85f, 0f, 0.35f);
        UnityEditor.Handles.DrawSolidArc(pivotWorld, axisWorld, closedDir, openAngle, arcRadius);

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(pivotWorld + axisWorld * 1.1f,                "  Axis");
        UnityEditor.Handles.Label(pivotWorld + closedDir * (arcRadius + 0.08f), "  Closed");
        UnityEditor.Handles.Label(pivotWorld + openDir   * (arcRadius + 0.08f), "  Open");

        string state = Application.isPlaying
            ? (_isLocked ? "LOCKED" : (_isOpen ? "OPEN" : "CLOSED"))
            : (startLocked ? "LOCKED" : (startOpen ? "OPEN" : "CLOSED"));

        string saveInfo = string.IsNullOrEmpty(saveKey) ? " [NO SAVE KEY]" : $" [{saveKey}]";
        UnityEditor.Handles.Label(pivotWorld + Vector3.up * 0.15f, $"  {state}{saveInfo}");

        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawLine(transform.position, pivotWorld);
    }
#endif
}