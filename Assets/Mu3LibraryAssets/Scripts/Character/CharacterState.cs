namespace Mu3Library.Character {
    public abstract class CharacterState {
        protected CharacterController character;



        public virtual void Init(CharacterController controller, object[] param = null) {
            character = controller;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}