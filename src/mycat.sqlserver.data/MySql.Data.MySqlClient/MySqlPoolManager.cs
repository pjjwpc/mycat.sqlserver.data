using MySql.Data.MySqlClient.Properties;
using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Principal;
using System.Threading;
namespace MySql.Data.MySqlClient
{
	internal class MySqlPoolManager
	{
		private static Dictionary<string, MySqlPool> pools;
		private static List<MySqlPool> clearingPools;
		internal static int maxConnectionIdleTime;
		private static Timer timer;
		static MySqlPoolManager()
		{
			MySqlPoolManager.pools = new Dictionary<string, MySqlPool>();
			MySqlPoolManager.clearingPools = new List<MySqlPool>();
			MySqlPoolManager.maxConnectionIdleTime = 180;
			MySqlPoolManager.timer = new Timer(new TimerCallback(MySqlPoolManager.CleanIdleConnections), null, MySqlPoolManager.maxConnectionIdleTime * 1000 + 8000, MySqlPoolManager.maxConnectionIdleTime * 1000);
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(MySqlPoolManager.EnsureClearingPools);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(MySqlPoolManager.EnsureClearingPools);
		}
		private static void EnsureClearingPools(object sender, EventArgs e)
		{
			MySqlPoolManager.ClearAllPools();
		}
		private static string GetKey(MySqlConnectionStringBuilder settings)
		{
			string text = "";
			lock (settings)
			{
				text = settings.ConnectionString;
			}
			if (settings.IntegratedSecurity && !settings.ConnectionReset)
			{
				try
				{
					WindowsIdentity current = WindowsIdentity.GetCurrent();
					text = text + ";" + current.User;
				}
				catch (SecurityException ex)
				{
					throw new MySqlException(Resources.NoWindowsIdentity, ex);
				}
			}
			return text;
		}
		public static MySqlPool GetPool(MySqlConnectionStringBuilder settings)
		{
			string key = MySqlPoolManager.GetKey(settings);
			Dictionary<string, MySqlPool> obj = MySqlPoolManager.pools;
			MySqlPool result;
			lock (obj)
			{
				MySqlPool mySqlPool;
				MySqlPoolManager.pools.TryGetValue(key, out mySqlPool);
				if (mySqlPool == null)
				{
					mySqlPool = new MySqlPool(settings);
					MySqlPoolManager.pools.Add(key, mySqlPool);
				}
				else
				{
					mySqlPool.Settings = settings;
				}
				result = mySqlPool;
			}
			return result;
		}
		public static void RemoveConnection(Driver driver)
		{
			MySqlPool pool = driver.Pool;
			if (pool == null)
			{
				return;
			}
			pool.RemoveConnection(driver);
		}
		public static void ReleaseConnection(Driver driver)
		{
			MySqlPool pool = driver.Pool;
			if (pool == null)
			{
				return;
			}
			pool.ReleaseConnection(driver);
		}
		public static void ClearPool(MySqlConnectionStringBuilder settings)
		{
			string key;
			try
			{
				key = MySqlPoolManager.GetKey(settings);
			}
			catch (MySqlException)
			{
				return;
			}
			MySqlPoolManager.ClearPoolByText(key);
		}
		private static void ClearPoolByText(string key)
		{
			Dictionary<string, MySqlPool> obj = MySqlPoolManager.pools;
			lock (obj)
			{
				if (MySqlPoolManager.pools.ContainsKey(key))
				{
					MySqlPool mySqlPool = MySqlPoolManager.pools[key];
					MySqlPoolManager.clearingPools.Add(mySqlPool);
					mySqlPool.Clear();
					MySqlPoolManager.pools.Remove(key);
				}
			}
		}
		public static void ClearAllPools()
		{
			Dictionary<string, MySqlPool> obj = MySqlPoolManager.pools;
			lock (obj)
			{
				List<string> list = new List<string>(MySqlPoolManager.pools.Count);
				foreach (string current in MySqlPoolManager.pools.Keys)
				{
					list.Add(current);
				}
				using (List<string>.Enumerator enumerator2 = list.GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						MySqlPoolManager.ClearPoolByText(enumerator2.Current);
					}
				}
			}
		}
		public static void RemoveClearedPool(MySqlPool pool)
		{
			MySqlPoolManager.clearingPools.Remove(pool);
		}
		public static void CleanIdleConnections(object obj)
		{
			List<Driver> list = new List<Driver>();
			Dictionary<string, MySqlPool> obj2 = MySqlPoolManager.pools;
			lock (obj2)
			{
				foreach (string current in MySqlPoolManager.pools.Keys)
				{
					MySqlPool mySqlPool = MySqlPoolManager.pools[current];
					list.AddRange(mySqlPool.RemoveOldIdleConnections());
				}
			}
			using (List<Driver>.Enumerator enumerator2 = list.GetEnumerator())
			{
				while (enumerator2.MoveNext())
				{
					enumerator2.Current.Close();
				}
			}
		}
	}
}
