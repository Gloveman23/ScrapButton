using Unity.Netcode;

namespace MysteryButton.Network{
    public class MyButtonNetworkManager : NetworkBehaviour{
        public static MyButtonNetworkManager instance;

        void Awake(){
            instance = this;
        }

    }
}