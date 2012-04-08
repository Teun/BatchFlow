using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BatchFlow
{
    internal static class Retry
    {
        public delegate T RetryDelegate<T>();
        public delegate void RetryDelegate();

        public delegate void RetryDelegateFailedHandler(Exception exception, int counter);
        public static event RetryDelegateFailedHandler RetryDelegateFailed;

        
        public static T Times<T>(RetryDelegate<T> del, int numberOfRetries, int msPause, bool throwExceptions)
        {
            int counter = 0;

        BeginLabel:
            try
            {
                counter++;
                return del.Invoke();
            }
            catch (Exception ex)
            {
                if (counter > numberOfRetries)
                {
                    if (throwExceptions) throw;
                    else return default(T);
                }
                else
                {
                    if (RetryDelegateFailed != null) RetryDelegateFailed(ex, counter);

                    Thread.Sleep(msPause);
                    goto BeginLabel;
                }
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="?"></param>
        /// <returns>False if failed, True if not failed</returns>
        public static bool Times(RetryDelegate del, int numberOfRetries, int msPause, bool throwExceptions)
        {
            int counter = 0;

        BeginLabel:
            try
            {
                counter++;
                del.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                if (counter > numberOfRetries)
                {
                    if (throwExceptions) throw;
                    else return false;
                }
                else
                {
                    if (RetryDelegateFailed != null) RetryDelegateFailed(ex, counter);

                    Thread.Sleep(msPause);
                    goto BeginLabel;
                }
            }
        }

    }
}
