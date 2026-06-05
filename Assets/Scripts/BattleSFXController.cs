using UnityEngine;

/// <summary>
/// 배틀씬 효과음 관리.
/// RhythmManager 오브젝트에 사전 배치되어 인스펙터에서 클립 할당.
/// </summary>
public class BattleSFXController : MonoBehaviour
{
    public static BattleSFXController Instance { get; private set; }

    [Header("Boss Direction")]
    [SerializeField] AudioClip _bossUp;
    [SerializeField] AudioClip _bossDown;
    [SerializeField] AudioClip _bossLeft;
    [SerializeField] AudioClip _bossRight;

    [Header("Player Direction")]
    [SerializeField] AudioClip _playerUp;
    [SerializeField] AudioClip _playerDown;
    [SerializeField] AudioClip _playerLeft;
    [SerializeField] AudioClip _playerRight;

    [Header("Judgement")]
    [SerializeField] AudioClip _hit;
    [SerializeField] AudioClip _miss;

    AudioSource _src;

    void Awake()
    {
        Instance = this;
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void PlayBossDir(ArrowDir dir)   => Play(BossClip(dir));
    public void PlayPlayerDir(ArrowDir dir) => Play(PlayerClip(dir));
    public void PlayHit()  => Play(_hit);
    public void PlayMiss() => Play(_miss);

    void Play(AudioClip clip)
    {
        if (clip == null || _src == null) return;
        _src.PlayOneShot(clip, SettingsManager.Volume);
    }

    AudioClip BossClip(ArrowDir dir) => dir switch
    {
        ArrowDir.Up    => _bossUp,
        ArrowDir.Down  => _bossDown,
        ArrowDir.Left  => _bossLeft,
        ArrowDir.Right => _bossRight,
        _              => null
    };

    AudioClip PlayerClip(ArrowDir dir) => dir switch
    {
        ArrowDir.Up    => _playerUp,
        ArrowDir.Down  => _playerDown,
        ArrowDir.Left  => _playerLeft,
        ArrowDir.Right => _playerRight,
        _              => null
    };
}
