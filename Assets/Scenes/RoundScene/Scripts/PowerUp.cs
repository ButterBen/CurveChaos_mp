using UnityEngine;

public class PowerUp : MonoBehaviour
{

    private PowerUpEffect[] possiblePowerUpEffects = new PowerUpEffect[] {
        new DoubleSpeed(),
        new HalfSpeed(),
        new InvertedControls()
    };

    private PowerUpEffect powerUpEffect;

    void Start() {
        InitPowerUp();
    }

    private void InitPowerUp() {
        int index = Random.Range(0, possiblePowerUpEffects.Length);
        powerUpEffect = possiblePowerUpEffects[index];

        string iconName = powerUpEffect.GetIconName();
        LoadSprite(iconName);
    }

    public PowerUpEffect Consume() {
        Destroy(gameObject);
        return powerUpEffect;
    }

    private void LoadSprite(string iconName) {
        Texture2D iconTexture= Resources.Load<Texture2D>("powerup/" + iconName);
        
        if (iconTexture != null) {
            Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), Vector2.one * 0.5f);
            GameObject icon = gameObject.transform.Find("Icon").gameObject;
            SpriteRenderer iconRenderer = icon.GetComponent<SpriteRenderer>();
            iconRenderer.sprite = iconSprite;
        } else {
            Debug.LogError("Texture '" + iconName + "' not found!");
        }
    }

}
