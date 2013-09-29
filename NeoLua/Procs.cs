using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public static class Procs
  {
    public static void FreeAndNil<T>(ref T obj)
      where T : class
    {
      IDisposable o = obj as IDisposable;
      if (o != null)
        o.Dispose();
      obj = null;
    } // proc FreeAndNil
  } // class Stuff
}
