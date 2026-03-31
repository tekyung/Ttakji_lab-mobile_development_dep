using TMPro;
using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    public TMP_Text txtMyLife;
    public TMP_Text txtEnemyLife;
    public TMP_Text txtMyResource;
    public TMP_Text txtEnemyResource;

    public int myLife = 5;
    public int enemyLife = 5;
    public int myResource = 0;
    public int enemyResource = 0;

    void Start()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (txtMyLife != null)
            txtMyLife.text = myLife.ToString();

        if (txtEnemyLife != null)
            txtEnemyLife.text = enemyLife.ToString();

        if (txtMyResource != null)
            txtMyResource.text = myResource.ToString();

        if (txtEnemyResource != null)
            txtEnemyResource.text = enemyResource.ToString();
    }

    public void AddMyResource(int amount)
    {
        myResource += amount;
        if (myResource < 0) myResource = 0;
        RefreshUI();
    }

    public void AddEnemyResource(int amount)
    {
        enemyResource += amount;
        if (enemyResource < 0) enemyResource = 0;
        RefreshUI();
    }

    public void DamageMyLife(int amount)
    {
        myLife -= amount;
        if (myLife < 0) myLife = 0;
        RefreshUI();
    }

    public void DamageEnemyLife(int amount)
    {
        enemyLife -= amount;
        if (enemyLife < 0) enemyLife = 0;
        RefreshUI();
    }
}