using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Score : MonoBehaviour
{
    
    public static float scoreValue=0;
    [SerializeField] Text score;

    void Start()
    {
        scoreValue=0;
        //score=GameObject.GetComponent<Text>();
        //score=GetComponent<Text>();
    }

    
    void Update()
    {

        scoreValue+=(Time.timeScale *1);
        score.text="Score: " + scoreValue;
       

    }
}
