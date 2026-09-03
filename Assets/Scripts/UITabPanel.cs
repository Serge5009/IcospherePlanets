using UnityEngine;

public abstract class UITabPanel : MonoBehaviour
{
    protected CelestialBody currentBody;
    protected int currentCellId = -1;

    public virtual void OnOpen()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public virtual void OnClose()
    {
        gameObject.SetActive(false);
    }

    public void ReceiveData(CelestialBody body, int cellId)
    {
        currentBody = body;
        currentCellId = cellId;
        if (gameObject.activeSelf)
        {
            Refresh();
        }
    }

    protected abstract void Refresh();
}