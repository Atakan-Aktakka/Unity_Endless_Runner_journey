using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;

public class UnityAds : MonoBehaviour,IUnityAdsListener

{
    // Start is called before the first frame update
   public bool TestMode= true;

   public string AndroidID="5034029";
   public string IOSID="5034028";

   private string RewardedID="Rewarded_Android";
   private string BannerID="Banner_Android";
   private string InterstitialID="Interstitial_Android";

   void Start(){
    Advertisement.AddListener(this);
    Advertisement.Initialize(AndroidID, TestMode);
   }
   public void ShowInterstitialAd(){
    if(Advertisement.IsReady(InterstitialID)){
        
        Advertisement.Show(InterstitialID);
    }
   }
   public void ShowRewardedAd(){
    if(Advertisement.IsReady(RewardedID)){
        Advertisement.Show(RewardedID);
    }
   }
   public void ShowBannerAd(){
    StartCoroutine(ShowBannerWhenReady());
   }
   IEnumerator ShowBannerWhenReady(){
    while(Advertisement.IsReady(BannerID)){
        yield return new WaitForSeconds(0.5f);
    }
    Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
    Advertisement.Show(BannerID);
   }

    public void OnUnityAdsReady(string placementId){
        
   }
     public void OnUnityAdsDidError(string message){
       
   }
     public void OnUnityAdsDidStart(string placementId){
        ;
   }
     public void OnUnityAdsDidFinish(string placementId, ShowResult showResult){
        
        if(placementId == RewardedID){
            switch(showResult){
                case ShowResult.Finished:
                    Debug.Log("Ads Finished Reward the player!");
                    return;
            }
        }
   }
}
