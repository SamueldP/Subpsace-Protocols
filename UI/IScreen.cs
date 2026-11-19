namespace SubspaceProtocols.UI;

public interface IScreen
{
    void OnEnter();
    void OnExit();
    void Update(float deltaTime, VirtualMouseState mouse);
    void DrawVirtual(VirtualMouseState mouse);
    void Unload();
}
