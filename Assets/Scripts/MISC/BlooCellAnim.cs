using UnityEngine;

public class BlooCellAnim : MonoBehaviour
{
    public float speed;
    private Vector2 pos;
    void Start()
    {
        pos = transform.position;
    }

    void Update()
    {
        pos.y -= speed * Time.deltaTime;
        transform.position = pos;
    }
}
