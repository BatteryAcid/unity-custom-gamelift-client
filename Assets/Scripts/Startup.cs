using UnityEngine;

public class Startup : MonoBehaviour
{
   public static string GameStatus = "LOADING";
   
   void Start()
   {
      GameLiftClient gameLiftClient = new GameLiftClient();
   }
}
