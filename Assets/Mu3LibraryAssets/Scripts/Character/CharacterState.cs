namespace Mu3Library.Character {
    public abstract class CharacterState {



        public virtual void Init(CharacterController controller, object[] param = null) {

        }

        public abstract void Enter();
        public abstract void Exit();
        public abstract void FixedUpdate();
        public abstract void Update();
        public abstract void LateUpdate();
    }
}