using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class IngameHUD : MonoBehaviour
{
    /// TIMER DISPLAY
    [Header("Timer Display")]
    public TMP_Text timerText;

    // Debug options
    [Header("Timer Debug")]
    [Tooltip("When true, timer begins immediately.")]
    public bool DebugTimer = false;

    // Invoked every frame the timer updates with the elapsed time (seconds).
    public event Action<float> OnTimerUpdated;

    private float _elapsedTime;
    private bool _isRunning;

    // True while the timer is running.
    public bool IsRunning => _isRunning;

    // Current elapsed time in seconds.
    public float ElapsedTime => _elapsedTime;

    void Start()
    {
        // initialize UI
        //UpdateTimerDisplay(_elapsedTime);
        UpdateScoreDisplays();
        UpdatePlayerDisplays();

        // Debug: start timer immediately
        if (DebugTimer)
            StartTimer();

        // Debug: add random score every second
        _debugScoreTimer = 0f;

        // Debug: health drain every second
        _debugHealthDrain = 0f;
    }

    void Update()
    {
        if (_isRunning)
        {
            _elapsedTime += Time.deltaTime;
            UpdateTimerDisplay(_elapsedTime);
            OnTimerUpdated?.Invoke(_elapsedTime);
        }

        // update score UI only when values change
        if (_lastScoreA != scoreA || _lastScoreB != scoreB)
        {
            UpdateScoreDisplays();
        }

        // update player UI only when values change
        if (_lastCurrentHealth != currentHealth || _lastMaxHealth != maxHealth || _lastCharacterClass != characterClass)
        {
            UpdatePlayerDisplays();
        }

        // Debug behavior: add random integer to both scores every second
        if (AddRandomScores)
        {
            _debugScoreTimer += Time.deltaTime;
            if (_debugScoreTimer >= 1f)
            {
                _debugScoreTimer -= 1f;
                int randA = UnityEngine.Random.Range(1, 6); // random integer from 1 to 5
                int randB = UnityEngine.Random.Range(2, 11); // random integer from 2 to 10
                AddScoreA(randA);
                AddScoreB(randB);
            }
        }
        // Debug behavior: reduce health by 5 every second
        if (DebugHealthDrain)
        {
            _debugHealthDrain += Time.deltaTime;
            if (_debugHealthDrain >= 1f)
            {
                _debugHealthDrain -= 1f;
                ModifyHealth(-5);
            }
        }
    }

    // Start the match timer from zero.
    public void StartTimer()
    {
        _elapsedTime = 0f;
        _isRunning = true;
        UpdateTimerDisplay(_elapsedTime);
        OnTimerUpdated?.Invoke(_elapsedTime);
    }

    // Pause the timer. Elapsed time is preserved.
    public void StopTimer()
    {
        _isRunning = false;
    }

    // Resume the timer if it was paused.
    public void ResumeTimer()
    {
        if (_elapsedTime >= 0f)
        {
            _isRunning = true;
        }
    }

    private void UpdateTimerDisplay(float seconds)
    {
        string text = FormatTime(seconds);

        if (timerText != null)
        {
            timerText.text = text;
        }
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int mins = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}", mins, secs);
    }

    /// SCORE DISPLAY
    [Header("Score Display")]
    public TMP_Text scoreAAmount;
    public TMP_Text scoreBAmount;

    private int scoreA;
    private int scoreB;

    // cached values to avoid unnecessary UI updates
    private int _lastScoreA = int.MinValue;
    private int _lastScoreB = int.MinValue;

    // Debug options
    [Header("Score Debug")]
    [Tooltip("When true, adds a random integer to both scores every second.")]
    public bool AddRandomScores = false;
    private float _debugScoreTimer;

    // Increment score A by amount (positive or negative).
    public void AddScoreA(int amount)
    {
        scoreA += amount;
        UpdateScoreDisplays();
    }

    // Increment score B by amount (positive or negative).
    public void AddScoreB(int amount)
    {
        scoreB += amount;
        UpdateScoreDisplays();
    }

    // Set score A explicitly.
    public void SetScoreA(int value)
    {
        scoreA = value;
        UpdateScoreDisplays();
    }

    // Set score B explicitly.
    public void SetScoreB(int value)
    {
        scoreB = value;
        UpdateScoreDisplays();
    }

    private void UpdateScoreDisplays()
    {
        if (scoreAAmount != null)
        {
            scoreAAmount.text = scoreA.ToString();
        }

        if (scoreBAmount != null)
        {
            scoreBAmount.text = scoreB.ToString();
        }

        _lastScoreA = scoreA;
        _lastScoreB = scoreB;
    }

    /// PLAYER DISPLAY
    [Header("Player Display")]
    [Tooltip("Text showing current health as 'current / max'.")]
    public TMP_Text healthText;

    [Tooltip("UI Slider to visualize health fraction.")]
    public Slider healthSlider;

    [Tooltip("Text displaying the player's character class.")]
    public TMP_Text classText;

    // Debug options
    [Header("Player Debug")]
    [Tooltip("When true, removes 5 health every second.")]
    public bool DebugHealthDrain = false;
    private float _debugHealthDrain;

    // health values (other scripts should call SetCurrentHealth/SetMaxHealth or ModifyHealth)
    private int currentHealth = 100;
    private int maxHealth = 100;

    // cached values to avoid unnecessary UI updates
    private int _lastCurrentHealth = int.MinValue;
    private int _lastMaxHealth = int.MinValue;
    private string _lastCharacterClass = string.Empty;

    // player's character class string
    private string characterClass = "Unknown";

    // Set current health (clamped between 0 and maxHealth).
    public void SetCurrentHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, Mathf.Max(1, maxHealth));
        UpdatePlayerDisplays();
    }

    // Modify current health by amount (positive or negative).
    public void ModifyHealth(int delta)
    {
        SetCurrentHealth(currentHealth + delta);
    }

    // Set maximum health. Current health will be clamped to the new max.
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdatePlayerDisplays();
    }

    // Set the character class string displayed on HUD.
    public void SetCharacterClass(string className)
    {
        characterClass = className ?? "Unknown";
        UpdatePlayerDisplays();
    }

    private void UpdatePlayerDisplays()
    {
        // Health text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }

        // Health slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = Mathf.Max(1, maxHealth);
            healthSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        // Character class text
        if (classText != null)
        {
            classText.text = characterClass;
        }

        // update cached values
        _lastCurrentHealth = currentHealth;
        _lastMaxHealth = maxHealth;
        _lastCharacterClass = characterClass;
    }
}