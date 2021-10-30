using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Amazon.CognitoIdentity;

// Largely based on: 
// https://github.com/aws-samples/amazon-gamelift-unity/blob/master/Assets/Scripts/GameLift.cs
public class GameLiftClient
{
   private AmazonGameLiftClient _amazonGameLiftClient;
   private BADNetworkClient _badNetworkClient;
   private static string IsProdArg = "-isProd"; // command line arg that indicates production build if present
   private string _playerUuid;
   private string CognitoIdentityPool = "us-east-1:YOUR_COGNITO_IDENTITY_POOL_ID";
   private string FleetId = "YOUR_FLEET_ID"; // TODO: probably don't hardcode this, use alias or something

   async private void CreatePlayerSession(GameSession gameSession)
   {
      PlayerSession playerSession = null;

      var maxRetryAttempts = 3;
      await RetryHelper.RetryOnExceptionAsync<Exception>
      (maxRetryAttempts, async () =>
      {
         playerSession = await CreatePlayerSessionAsync(gameSession);
      });

      if (playerSession != null)
      {
         // created a player session in there
         Debug.Log("Player session created.");
         Debug.Log($"CLIENT CONNECT INFO: {playerSession.IpAddress}, {playerSession.Port}, {playerSession.PlayerSessionId} ");

         // establish connection with server
         _badNetworkClient.ConnectToServer(playerSession.IpAddress, playerSession.Port, playerSession.PlayerSessionId);
      }

   }

   async private Task<PlayerSession> CreatePlayerSessionAsync(GameSession gameSession)
   {
      var createPlayerSessionRequest = new CreatePlayerSessionRequest();
      createPlayerSessionRequest.GameSessionId = gameSession.GameSessionId;
      createPlayerSessionRequest.PlayerId = _playerUuid;

      Task<CreatePlayerSessionResponse> createPlayerSessionResponseTask = _amazonGameLiftClient.CreatePlayerSessionAsync(createPlayerSessionRequest);
      CreatePlayerSessionResponse createPlayerSessionResponse = await createPlayerSessionResponseTask;

      string playerSessionId = createPlayerSessionResponse.PlayerSession != null ? createPlayerSessionResponse.PlayerSession.PlayerSessionId : "N/A";
      Debug.Log((int)createPlayerSessionResponse.HttpStatusCode + " PLAYER SESSION CREATED: " + playerSessionId);
      return createPlayerSessionResponse.PlayerSession;
   }

   async private Task<GameSession> CreateGameSessionAsync()
   {
      Debug.Log("CreateGameSessionAsync");
      var createGameSessionRequest = new Amazon.GameLift.Model.CreateGameSessionRequest();
      createGameSessionRequest.FleetId = FleetId; // can also use AliasId
      createGameSessionRequest.CreatorId = _playerUuid;
      createGameSessionRequest.MaximumPlayerSessionCount = 2; // search for two player game

      Task<CreateGameSessionResponse> createGameSessionRequestTask = _amazonGameLiftClient.CreateGameSessionAsync(createGameSessionRequest);
      Debug.Log("after task createGameSessionRequestTask");
      CreateGameSessionResponse createGameSessionResponse = await createGameSessionRequestTask;
      Debug.Log("after createGameSessionRequestTask");

      string gameSessionId = createGameSessionResponse.GameSession != null ? createGameSessionResponse.GameSession.GameSessionId : "N/A";
      Debug.Log((int)createGameSessionResponse.HttpStatusCode + " GAME SESSION CREATED: " + gameSessionId);

      return createGameSessionResponse.GameSession;
   }

   // Start here to find open single player game sessions to join
   async private Task<GameSession> SearchGameSessionsAsync()
   {
      Debug.Log("SearchGameSessions");
      var searchGameSessionsRequest = new SearchGameSessionsRequest();
      searchGameSessionsRequest.FleetId = FleetId; // can also use AliasId
      searchGameSessionsRequest.FilterExpression = "hasAvailablePlayerSessions=true"; // only ones we can join
      searchGameSessionsRequest.SortExpression = "creationTimeMillis ASC"; // return oldest first
      searchGameSessionsRequest.Limit = 1; // only one session even if there are other valid ones

      Task<SearchGameSessionsResponse> SearchGameSessionsResponseTask = _amazonGameLiftClient.SearchGameSessionsAsync(searchGameSessionsRequest);
      SearchGameSessionsResponse searchGameSessionsResponse = await SearchGameSessionsResponseTask;

      int gameSessionCount = searchGameSessionsResponse.GameSessions.Count;
      Debug.Log($"GameSessionCount:  {gameSessionCount}");

      if (gameSessionCount > 0)
      {
         Debug.Log("We have game sessions!");
         Debug.Log(searchGameSessionsResponse.GameSessions[0].GameSessionId);
         return searchGameSessionsResponse.GameSessions[0];
      }
      return null;
   }

   //TODO: we need to query for all the available queues, that way we're not hardcoding anything in our code.
   // So the client will query for queues, with some filter on tag or name or something to make sure we're not getting one that's "disabled".
   // Then we pass the queue to StartGameSessionPlacement
   //

   // NEW for queue research
   // async private Task<string> SearchQueuesAsync()
   // {
   //    List<string> queueNames = new List<string> { "29OCT2021-queue" };

   //    DescribeGameSessionQueuesRequest describeGameSessionQueuesRequest = new DescribeGameSessionQueuesRequest();
   //    describeGameSessionQueuesRequest.Names = queueNames;

   //    Task<DescribeGameSessionQueuesResponse> describeGameSessionQueuesResponseTask = _amazonGameLiftClient.DescribeGameSessionQueuesAsync(describeGameSessionQueuesRequest);
   //    DescribeGameSessionQueuesResponse describeGameSessionQueuesResponse = await describeGameSessionQueuesResponseTask;

   //    GameSessionQueue gameSessionQueue = describeGameSessionQueuesResponse.GameSessionQueues[0];
   //    if (gameSessionQueue != null && gameSessionQueue.Destinations.Count > 0)
   //    {
   //       Debug.Log("Found destinations, ARN: " + gameSessionQueue.Destinations[0].DestinationArn);


   //    }
   //    else
   //    {
   //       Debug.Log("Game session queue was null or no destinations returned");
   //    }
   //    //.Destinations[0]

   //    return null;
   // }

   // NEW for queue research
   // async private Task<GameSession> SearchGameSessionsAsync(string alias)
   // {

   //    return null;
   // }

   // New for queue research
   async private Task<GameSessionPlacement> SearchGameSessionPlacementAsync(string queueName, string placementId)
   {
      StartGameSessionPlacementRequest startGameSessionPlacementRequest = new StartGameSessionPlacementRequest();
      startGameSessionPlacementRequest.PlacementId = placementId;
      startGameSessionPlacementRequest.GameSessionQueueName = queueName;
      startGameSessionPlacementRequest.MaximumPlayerSessionCount = 8;

      // desired sessions for ONE player
      // player id is required here to get the player session id on the other end
      startGameSessionPlacementRequest.DesiredPlayerSessions = new List<DesiredPlayerSession> { new DesiredPlayerSession { PlayerId = _playerUuid } };

      // NOTE: according to the docs this is how you can place multiple players into a game session: https://docs.aws.amazon.com/gamelift/latest/apireference/API_StartGameSessionPlacement.html
      // This actually creates player sessions
      Task<StartGameSessionPlacementResponse> startGameSessionPlacementResponseTask = _amazonGameLiftClient.StartGameSessionPlacementAsync(startGameSessionPlacementRequest);
      StartGameSessionPlacementResponse startGameSessionPlacementResponse = await startGameSessionPlacementResponseTask;
      Debug.Log(startGameSessionPlacementResponse.GameSessionPlacement.Status);

      Debug.Log(startGameSessionPlacementResponse.HttpStatusCode);
      return startGameSessionPlacementResponse.GameSessionPlacement;
   }

   // New for queue research, for single player joining. Will probably have to have different route for group joins
   async private void FindMatch()
   {
      GameSession gameSession = IsArgFlagPresent(IsProdArg) ? await SearchGameSessionsAsync() : null;

      if (gameSession == null)
      {
         Debug.Log("No Game sessions found.");
         string placementId = Guid.NewGuid().ToString();

         // TODO: pull this queue from a Queue Describe query
         GameSessionPlacement gameSessionPlacement = await SearchGameSessionPlacementAsync("29OCT2021-queue", placementId);

         // TODO: ONLY FOR LOCAL TESTING, DONT DO THIS, we need to setup SNS topic or something for production deployments
         // source: https://docs.aws.amazon.com/gamelift/latest/apireference/API_DescribeGameSessionPlacement.html
         // TODO: wrap this in editor only defines
         // or maybe a back off strategy? 
         string placementStatus = "";
         while (placementStatus != "FULFILLED")
         {
            gameSessionPlacement = CheckPlayerSessionPlacementStatus(placementId);
            placementStatus = gameSessionPlacement.Status;
            Debug.Log(placementStatus);
         }

         Debug.Log($"CLIENT CONNECT INFO: {gameSessionPlacement.IpAddress}, {gameSessionPlacement.Port}, {gameSessionPlacement.PlacedPlayerSessions[0].PlayerSessionId} ");

         // establish connection with server
         _badNetworkClient.ConnectToServer(gameSessionPlacement.IpAddress, gameSessionPlacement.Port, gameSessionPlacement.PlacedPlayerSessions[0].PlayerSessionId);
      }
      else
      {
         Debug.Log("Game session found.");

         // game session found, create player session and connect to server
         CreatePlayerSession(gameSession);
      }
   }

   private GameSessionPlacement CheckPlayerSessionPlacementStatus(string placementId)
   {
      DescribeGameSessionPlacementRequest describeQueuePlacementStatusRequest = new DescribeGameSessionPlacementRequest();
      describeQueuePlacementStatusRequest.PlacementId = placementId;

      DescribeGameSessionPlacementResponse describeQueuePlacementStatusResponse = _amazonGameLiftClient.DescribeGameSessionPlacementAsync(describeQueuePlacementStatusRequest).Result;
      return describeQueuePlacementStatusResponse.GameSessionPlacement;
   }


   async private void setup()
   {
      Debug.Log("setup");

      _badNetworkClient = GameObject.FindObjectOfType<BADNetworkClient>();

      CreateGameLiftClient();

      FindMatch();
      // FindMatchOriginal();
   }

   // This was my original function
   async private void FindMatchOriginal()
   {
      // Mock game session queries aren't implemented for local GameLift server testing, so just return null to create new one
      GameSession gameSession = IsArgFlagPresent(IsProdArg) ? await SearchGameSessionsAsync() : null;

      if (gameSession == null)
      {
         // create one game session
         var maxRetryAttempts = 3;
         await RetryHelper.RetryOnExceptionAsync<Exception>
         (maxRetryAttempts, async () =>
         {
            gameSession = await CreateGameSessionAsync();
         });

         if (gameSession != null)
         {
            Debug.Log("Game session created.");
            CreatePlayerSession(gameSession);
         }
         else
         {
            Debug.LogWarning("FAILED to create new game session.");
         }
      }
      else
      {
         Debug.Log("Game session found.");

         // game session found, create player session and connect to server
         CreatePlayerSession(gameSession);
      }
   }

   private void CreateGameLiftClient()
   {
      Debug.Log("CreateGameLiftClient");

      CognitoAWSCredentials credentials = new CognitoAWSCredentials(
         CognitoIdentityPool,
         RegionEndpoint.USEast1
      );

      if (IsArgFlagPresent(IsProdArg))
      {
         _amazonGameLiftClient = new AmazonGameLiftClient(credentials, RegionEndpoint.USEast1);
      }
      else
      {
         // local testing
         // guide: https://docs.aws.amazon.com/gamelift/latest/developerguide/integration-testing-local.html
         AmazonGameLiftConfig amazonGameLiftConfig = new AmazonGameLiftConfig()
         {
            ServiceURL = "http://localhost:9080"
         };
         _amazonGameLiftClient = new AmazonGameLiftClient("asdfasdf", "asdf", amazonGameLiftConfig);
      }
   }

   public GameLiftClient()
   {
      Debug.Log("GameLiftClient created");

      // for this demo just create a randomly generated user id.  Eventually the ID may be tied to a user account.
      _playerUuid = Guid.NewGuid().ToString();

      setup();
   }

   // Helper function for getting the command line arguments
   // src: https://stackoverflow.com/a/45578115/1956540
   public static bool IsArgFlagPresent(string name)
   {
      var args = System.Environment.GetCommandLineArgs();
      for (int i = 0; i < args.Length; i++)
      {
         // Debug.Log("Arg: " + args[i]);
         if (args[i] == name)
         {
            return true;
         }
      }
      return false;
   }
}
