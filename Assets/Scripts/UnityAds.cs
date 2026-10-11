using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;

public class UnityAds : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener

{
    // Start is called before the first frame update
   public bool TestMode= true;

   public string AndroidID="5034029";
   public string IOSID="5034028";

   private string RewardedID="Rewarded_Android";
   private string BannerID="Banner_Android";
   private string InterstitialID="Interstitial_Android";

   private readonly HashSet<string> loadedAds = new HashSet<string>();
   private System.Action onInterstitialFinished;

   void Start(){
    Advertisement.Initialize(AndroidID, TestMode, this);
   }
   public void ShowInterstitialAd(){
    if(loadedAds.Remove(InterstitialID)){
        Advertisement.Show(InterstitialID, this);
    }
   }
   // Shows the interstitial and calls onFinished when it closes; returns false if no ad is ready
   public bool ShowInterstitialAd(System.Action onFinished){
    if(!loadedAds.Remove(InterstitialID)){
        return false;
    }
    onInterstitialFinished = onFinished;
    Advertisement.Show(InterstitialID, this);
    return true;
   }
   private void FinishInterstitial(string placementId){
    if(placementId != InterstitialID || onInterstitialFinished == null){
        return;
    }
    System.Action callback = onInterstitialFinished;
    onInterstitialFinished = null;
    callback();
   }
   public void ShowRewardedAd(){
    if(loadedAds.Remove(RewardedID)){
        Advertisement.Show(RewardedID, this);
    }
   }
   public void ShowBannerAd(){
    Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
    Advertisement.Banner.Load(BannerID, new BannerLoadOptions{
        loadCallback = () => Advertisement.Banner.Show(BannerID)
    });
   }

    public void OnInitializationComplete(){
        Advertisement.Load(InterstitialID, this);
        Advertisement.Load(RewardedID, this);
   }
     public void OnInitializationFailed(UnityAdsInitializationError error, string message){
        Debug.Log("Unity Ads init failed: " + error + " " + message);
   }
     public void OnUnityAdsAdLoaded(string placementId){
        loadedAds.Add(placementId);
   }
     public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message){
        Debug.Log("Unity Ads load failed: " + placementId + " " + message);
   }
     public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message){
        Advertisement.Load(placementId, this);
        FinishInterstitial(placementId);
   }
     public void OnUnityAdsShowStart(string placementId){
   }
     public void OnUnityAdsShowClick(string placementId){
   }
     public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState){
        if(placementId == RewardedID && showCompletionState == UnityAdsShowCompletionState.COMPLETED){
            Debug.Log("Ads Finished Reward the player!");
        }
        Advertisement.Load(placementId, this);
        FinishInterstitial(placementId);
   }
}
