using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public GameObject Camera;
    public GameObject Golem_Boss;
    public GameObject Player;
    public GameObject MenuPanel;
    public GameObject GamePanel;

    public GameObject Title;
    public GameObject Option;
    public GameObject Keysetting;
    public Image Guard;
    public Image Heal;
    public Image Buff;
    public Image Ultimate;
    public RectTransform PlayerHP;
    public RectTransform BossHP;
    public PlayerHealth playerHealth;
    public BossHealth bossHealth;

    void Awake()
    {
        if (bossHealth != null) bossHealth.hpFill = BossHP;
        if (playerHealth != null) playerHealth.hpFill = PlayerHP; 
    }

    public void GameStart()
    {
        Camera.SetActive(false);

        Golem_Boss.SetActive(true);
        Player.SetActive(true);

        MenuPanel.SetActive(false);
        GamePanel.SetActive(true);
    }

    public void GameOption()
    {
        Title.SetActive(false);
        Option.SetActive(true);
    }

    public void GameOptionR()
    {
        Title.SetActive(true);
        Option.SetActive(false);
    }

    public void GameKeySetting()
    {
        Title.SetActive(false);
        Keysetting.SetActive(true);
    }

    public void GameKeySettingR()
    {
        Title.SetActive(true);
        Keysetting.SetActive(false);
    }

}
