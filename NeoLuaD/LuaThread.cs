using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Neo.IronLua
{
  #region -- enum LuaThreadStatus -----------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Descripes the status of an LuaThread.</summary>
  public enum LuaThreadStatus
  {
    /// <summary>Coroutine is active but not running.</summary>
    Normal,
    /// <summary>Coroutine is running.</summary>
    Running,
    /// <summary>Coroutine currently calls yield.</summary>
    Suspended,
    /// <summary>Coroutine has stopped.</summary>
    Dead
  } // enum LuaThreadStatus

  #endregion

  #region -- class LuaThread ----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Implemention of the coroutine module.</summary>
  public sealed class LuaThread : IAsyncResult, IDisposable
  {
    private const string csNormal = "normal";
    private const string csRunning = "running";
    private const string csSuspended = "suspended";
    private const string csDead = "dead";

    private static List<LuaThread> luaThreads = new List<LuaThread>();

    private object target;
    private LuaResult currentArguments = null;
    private LuaResult currentYield = null;
    private ManualResetEventSlim evYield = null;
    private ManualResetEventSlim evResume = null;
    private object lockResume = null;
    private Action procExecute;
    private IAsyncResult arExecute = null;

    private int iCoroutineThreadId = -1;

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Creates a new lua thread.</summary>
		/// <param name="target">Delegate that should run as a thread.</param>
		public LuaThread(object target)
    {
			this.target = target;
      this.procExecute = ExecuteDelegate;
    } // ctor

    /// <summary>Gibt die Daten frei</summary>
    public void Dispose()
    {
      currentArguments = null;
      currentArguments = null;
      if (evYield != null)
      {
        evYield.Dispose();
        evYield = null;
      }
      if (evResume != null)
      {
        evResume.Set();
        evResume.Dispose();
        evResume = null;
      }
    } // proc Dispose
    
    /// <summary>Creates a coroutine from an delegate.</summary>
		/// <param name="target"></param>
    /// <returns></returns>
    public static LuaThread create(object target)
    {
			return new LuaThread(target);
    } // func create

    #endregion

    #region -- Resume -----------------------------------------------------------------

    /// <summary>Starts the execution of the next part of the thread.</summary>
    /// <param name="args">Arguments, that will pass to the function-part</param>
    /// <returns>Returns the <c>LuaThread</c> as an IAsyncResult.</returns>
    public IAsyncResult BeginResume(object[] args)
    {
      // Lock the resume to only one pair Begin/End
      lock (this)
      {
        if (lockResume != null)
          throw new InvalidOperationException();
        
        lockResume = new object();
        Monitor.Enter(lockResume);
      }

      // set parameters
      currentArguments = args;

      // start the coroutine, if not startet
      if (arExecute == null)
      {
        evYield = new ManualResetEventSlim(false);

        arExecute = procExecute.BeginInvoke(EndExecuteDelegate, null);
      }
      else if (evResume != null)
      {
        evYield.Reset();
        evResume.Set(); // resume the coroutine
      }

      return this;
    } // proc resumeAsync

    /// <summary></summary>
    /// <param name="ar"></param>
    /// <returns></returns>
    public LuaResult EndResume(IAsyncResult ar)
    {
      lock (this)
        try
        {
          if (this != ar)
            throw new LuaRuntimeException(Properties.Resources.rsCoroutineInvalidAR, 2, false);

          if (lockResume == null)
            throw new LuaRuntimeException(Properties.Resources.rsCoroutineNoBeginResume, 2, false);

          if (evYield != null)
            evYield.Wait();

          if (evResume == null)
          {
            evYield.Dispose();
            evYield = null;
            return new LuaResult(false, "cannot resume dead coroutine");
          }
          else
            return new LuaResult(true, currentYield);
        }
        finally
        {
          Monitor.Exit(lockResume);
          lockResume = null;
        }
    } // proc EndResume

    /// <summary>Executes a part of the thread.</summary>
    /// <param name="args">Arguments, that will pass to the function-part</param>
    /// <returns>Result of the function.</returns>
    public LuaResult resume(object[] args)
    {
      return EndResume(BeginResume(args));
    } // func Resume

    /// <summary>Executes a part of the thread.</summary>
    /// <param name="co">Thread, that should resume.</param>
    /// <param name="args">Arguments, that will pass to the function-part</param>
    /// <returns>Result of the function.</returns>
    public static LuaResult resume(LuaThread co, object[] args)
    {
      return co.resume(args);
    } // func resume

    #endregion

    #region -- yield ------------------------------------------------------------------

    /// <summary>Sends a result to the main-thread.</summary>
    /// <param name="args">Result</param>
    /// <returns>New parameter.</returns>
    public static LuaResult yield(object[] args)
    {
      // search for the correct thread
      LuaThread t = null;
      lock (luaThreads)
        t = luaThreads.Find(c => c.iCoroutineThreadId == Thread.CurrentThread.ManagedThreadId);
      if (t == null)
        throw new LuaRuntimeException(Properties.Resources.rsCoroutineWrongThread, 2, false);

      // yield value
      t.currentYield = args;
      t.evYield.Set();
      // wait for next arguments
      t.evResume.Reset();
      t.evResume.Wait();
      // return the arguments or stop the background thread
      if (t.evResume == null)
        Thread.CurrentThread.Abort();
      if (t.currentArguments != null)
        return new LuaResult(t.currentArguments);
      else
        return LuaResult.Empty;
    } // func Yield

    #endregion

    #region -- ExecuteDelegate --------------------------------------------------------

    private void ExecuteDelegate()
    {
      evResume = new ManualResetEventSlim(false);
       
      // add this thread to the thread-pool
      iCoroutineThreadId = Thread.CurrentThread.ManagedThreadId;
      lock (luaThreads)
        luaThreads.Add(this);

      yield(new LuaResult(Lua.RtInvoke(target, currentArguments.Values)));
    } // proc ExecuteDelegate

    private void EndExecuteDelegate(IAsyncResult ar)
    {
      try
      {
        procExecute.EndInvoke(ar);
      }
      catch
      {
      }

      // remove the thread from the pool
      lock (luaThreads)
        luaThreads.Remove(this);

      iCoroutineThreadId = -1;
      
      // dispose the events
      currentYield = LuaResult.Empty;
      evResume.Dispose();
      evResume = null;
      evYield.Set();
    } // proc EndExecuteDelegate

    #endregion

    #region -- IAsyncResult members ---------------------------------------------------

    object IAsyncResult.AsyncState { get { return this; } }
    WaitHandle IAsyncResult.AsyncWaitHandle { get { return evYield.WaitHandle; } }
    bool IAsyncResult.CompletedSynchronously { get { return evYield == null; } }
    bool IAsyncResult.IsCompleted { get { return evYield == null ? true : evYield.IsSet; } }

    #endregion

    #region -- running, status, wrap --------------------------------------------------

    /// <summary>Returns the running coroutine and a boolean if it is the main.</summary>
    /// <returns></returns>
    public static LuaResult running()
    {
      LuaThread t = null;
      lock (luaThreads)
        t = luaThreads.Find(c => c.iCoroutineThreadId == Thread.CurrentThread.ManagedThreadId);

      if (t == null)
        return new LuaResult(Thread.CurrentThread, true);
      else
        return new LuaResult(t, false);
    } // func running

    /// <summary>Returns the status of the thread.</summary>
    public LuaThreadStatus Status
    {
      get
      {
        if (iCoroutineThreadId == Thread.CurrentThread.ManagedThreadId)
          return LuaThreadStatus.Running;
        else
        {
					/*
					 * "running", if the coroutine is running (that is, it called status); 
					 * "suspended", if the coroutine is suspended in a call to yield, or if it has not started running yet; 
					 * "normal" if the coroutine is active but not running (that is, it has resumed another coroutine); and 
					 * "dead" if the coroutine has finished its body function, or if it has stopped with an error. 
					 */
					if (evYield == null) // is the coroutine active
						return arExecute == null ? // is the routine started
							LuaThreadStatus.Suspended : // no, suspended
							LuaThreadStatus.Dead; // yes, dead
					else 
						return evYield.IsSet ? // atkive coroutine yields a values
							LuaThreadStatus.Suspended : // currently in the yield
							LuaThreadStatus.Normal; // not in the yield, running or waiting?
        }
      }
    } // prop Status

    /// <summary>Returns the status of the thread.</summary>
    /// <param name="co">Thread</param>
    /// <returns>Returns the status.</returns>
    public static LuaResult status(object co)
    {
      LuaThread luaThread = co as LuaThread;
      Thread clrThread = co as Thread;
      if (luaThread != null)
        switch (luaThread.Status)
        {
          case LuaThreadStatus.Dead:
            return new LuaResult(csDead);
          case LuaThreadStatus.Normal:
            return new LuaResult(csNormal);
          case LuaThreadStatus.Running:
            return new LuaResult(csRunning);
          case LuaThreadStatus.Suspended:
            return new LuaResult(csSuspended);
          default:
            throw new LuaRuntimeException("thead-status invalid", 2, false);
        }
      else if (clrThread != null)
      {
        ThreadState s = clrThread.ThreadState;
        if ((s & (ThreadState.Aborted | ThreadState.AbortRequested | ThreadState.Stopped | ThreadState.StopRequested)) != 0)
          return new LuaResult(csDead);
        else if ((s & (ThreadState.Suspended | ThreadState.SuspendRequested | ThreadState.WaitSleepJoin)) != 0)
          return new LuaResult(csSuspended);
        else if ((s & ThreadState.Unstarted) != 0)
          return new LuaResult(csNormal);
        else
          return new LuaResult(csRunning);
      }
      else
        throw new LuaRuntimeException(Properties.Resources.rsCoroutineInvalidCO, 2, false);
    } // func running

    /// <summary>Creates a function, that wraps resume.</summary>
    /// <param name="dlg"></param>
    /// <returns></returns>
    public static Func<object[], LuaResult> wrap(Delegate dlg)
    {
      LuaThread t = new LuaThread(dlg);
      return new Func<object[], LuaResult>(args => t.resume(args));
    } // func wrap

    #endregion
  } // class LuaThread

  #endregion
}
