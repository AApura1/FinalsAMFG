using UnityEngine;
using TMPro;

public class HPUI : MonoBehaviour
{
    public EnhancedMeshGenerator game;
    public TMP_Text text;

    void Update()
    {
        text.text = "HP: " + game.hp;
    }
}