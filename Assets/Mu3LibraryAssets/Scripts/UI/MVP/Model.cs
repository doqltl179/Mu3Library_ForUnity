namespace Mu3Library.UI.MVP
{
    public abstract class Model<TArgs> where TArgs : Arguments
    {



        public abstract void Init(TArgs args);
    }
}
