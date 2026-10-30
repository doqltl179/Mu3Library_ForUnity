using Mu3Library.Observable;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Holds Observables on a component that the EditMode tests save into a prefab. A prefab keeps
    /// a component only through a script file named after it, and <c>AddComponent</c> refuses one
    /// from an editor assembly, so it lives in this assembly rather than with those tests.
    /// </summary>
    public class ObservablePrefabTestComponent : MonoBehaviour
    {
        [SerializeField] private ObservableInt _health = new ObservableInt();
        [SerializeField] private ObservableInt _mana = new ObservableInt();

        public ObservableInt Health => _health;
        public ObservableInt Mana => _mana;
    }
}
