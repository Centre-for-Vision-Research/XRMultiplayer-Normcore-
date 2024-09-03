using UnityEngine;

/// <summary>
/// Changes the color of a gameobject on collision
/// </summary>
public class CollisionTrigger : MonoBehaviour
{
    [SerializeField]
    Color original = new Color(0, 0, 102);
    [SerializeField]
    Color touching = new Color(0, 204, 102);

    private void Start()
    {
        setColor(original);
    }

    // TODO dirty buggy code. need to access all children recursively.
    private void setColor(Color c)
    {
        if (gameObject.GetComponent<Renderer>() != null)
        {
            gameObject.GetComponent<Renderer>().material.color = c;
        }
        else
        {
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                if (gameObject.transform.GetChild(i).GetComponent<Renderer>() != null)
                {
                    gameObject.transform.GetChild(i).GetComponent<Renderer>().material.color = c;
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        setColor(touching);
    }

    private void OnCollisionExit(Collision collision)
    {
        setColor(original);
    }

    private void OnTriggerEnter(Collider other)
    {
        setColor(touching);
    }

    private void OnTriggerExit(Collider other)
    {
        setColor(original);
    }
}
