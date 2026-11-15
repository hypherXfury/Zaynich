using UnityEngine;

public class Plasma : MonoBehaviour
{
    private FourDirPreviewController controller;
    private float originalSpeed;

    void Start()
    {
        controller = FindFirstObjectByType<FourDirPreviewController>();
        originalSpeed = controller.MoveSpeed;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            controller.SetMoveSpeed(.25f);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            controller.SetMoveSpeed(originalSpeed);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            controller.SetMoveSpeed(.25f);
        }
    }
}
