using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Mostly based on:
// https://alastaircrabtree.com/implementing-the-retry-pattern-for-async-tasks-in-c/
// https://docs.microsoft.com/en-us/azure/architecture/patterns/retry
public static class RetryHelper
{
   public static async Task RetryOnExceptionAsync<TException>(int maxAttempts, Func<Task> operation) where TException : Exception
   {
      if (maxAttempts <= 0)
         throw new ArgumentOutOfRangeException(nameof(maxAttempts));

      var attempts = 0;
      TException exception;
      do
      {
         try
         {
            Debug.Log("Attempt #" + attempts);
            await operation();
            break;
         }
         catch (TException ex)
         {
            Debug.Log("RetryHelper Exception encountered: " + ex.Message);

            if (attempts == maxAttempts)
               throw;

            exception = ex;
         }

         attempts++;
         await CreateDelayForException(maxAttempts, attempts, exception);
         
      } while (true);
   }

   private static Task CreateDelayForException(int maxAttempts, int attempts, Exception ex)
   {
      var nextDelay = IncreasingDelayInSeconds(attempts - 1);
      Debug.Log($"Exception on attempt {attempts} of {maxAttempts}. Will retry after sleeping for {nextDelay}. " + ex.Message);
      return Task.Delay(nextDelay);
   }

   static TimeSpan IncreasingDelayInSeconds(int failedAttempts)
   {
      if (failedAttempts < 0) throw new ArgumentOutOfRangeException();

      return failedAttempts > DelayPerAttemptInSeconds.Length ? DelayPerAttemptInSeconds.Last() : DelayPerAttemptInSeconds[failedAttempts];
   }

   internal static TimeSpan[] DelayPerAttemptInSeconds =
   {
      TimeSpan.FromSeconds(2),
      TimeSpan.FromSeconds(3),
      TimeSpan.FromSeconds(5)
   };
}
