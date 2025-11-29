namespace Mu3Library.UI.MVP
{
    public abstract class Model<TArgs> where TArgs : Arguments
    {



        public virtual void Init(TArgs args) { }
    }
}
