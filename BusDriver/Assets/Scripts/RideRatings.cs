using System;
using UnityEngine;

// The driver's star rating and drop-off quota for the shift. Dropping under the
// rating threshold ends the run; hitting the quota opens the next-level screen.
public class RideRatings : MonoBehaviour {
    [Header("Average")]
    [Tooltip("Reviews the shift starts with, so the very first bad review cannot end it alone")]
    [SerializeField] int seedReviews = 1;
    [SerializeField] float seedStars = 5f;
    [SerializeField] float gameOverBelow = 2.5f;

    [Header("Quota")]
    [Tooltip("Passengers that must be dropped off (not kicked) to clear the shift")]
    [SerializeField] int dropOffQuota = 5;

    [Header("Scoring")]
    [Tooltip("Average speed (km/h) a five star trip is expected to keep")]
    [SerializeField] float expectedSpeedKmh = 25f;
    [Tooltip("Slack for boarding, door time and getting rolling")]
    [SerializeField] float graceSeconds = 25f;
    [Tooltip("Stars lost per scrape while they were aboard, doubled for a big hit")]
    [SerializeField] int crashPenalty = 1;

    float totalStars;
    int reviewCount;
    int dropOffsCompleted;

    public float Average { get { return reviewCount > 0 ? totalStars / reviewCount : seedStars; } }
    public int ReviewCount { get { return reviewCount; } }
    public int DropOffsCompleted { get { return dropOffsCompleted; } }
    public int DropOffQuota { get { return Mathf.Max(1, dropOffQuota); } }
    public bool QuotaMet { get { return dropOffsCompleted >= DropOffQuota; } }
    public event Action OnChanged;

    void Awake() {
        reviewCount = Mathf.Max(0, seedReviews);
        totalStars = seedStars * reviewCount;
        dropOffsCompleted = 0;
    }

    public void AddReview(int stars) {
        totalStars += Mathf.Clamp(stars, 0, 5);
        reviewCount++;
        OnChanged?.Invoke();
        if (Average < gameOverBelow && SceneController.Instance != null) {
            SceneController.Instance.TriggerGameOver();
        }
    }

    // Counted when a passenger leaves at a stop (missed or on time). Kicks do not count.
    public void RecordDropOff() {
        if (QuotaMet) {
            return;
        }
        dropOffsCompleted++;
        OnChanged?.Invoke();
        if (QuotaMet && SceneController.Instance != null) {
            SceneController.Instance.TriggerQuotaFulfilled();
        }
    }

    // Five stars for arriving inside the expected time, less for dawdling or crashing.
    // Never zero: a zero is reserved for a missed stop or a kick.
    public int ScoreRide(float routeDistance, float elapsedSeconds, int crashPoints) {
        float expected = graceSeconds + routeDistance / Mathf.Max(1f, expectedSpeedKmh / 3.6f);
        float ratio = elapsedSeconds / Mathf.Max(1f, expected);
        int stars = 5;
        if (ratio > 2.5f) {
            stars = 2;
        }else if (ratio > 1.75f) {
            stars = 3;
        }else if (ratio > 1.25f) {
            stars = 4;
        }
        stars -= crashPoints * crashPenalty;
        return Mathf.Clamp(stars, 1, 5);
    }
}
