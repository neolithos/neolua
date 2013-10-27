using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaFile
  {
    /// <summary></summary>
    /// <returns></returns>
    public object[] close()
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    public void flush()
    {
    }

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public object[] lines(object[] args)
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public object[] read(object[] args)
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    /// <param name="whence"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public long seek(string whence, long offset = 0)
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    /// <param name="mode"></param>
    /// <param name="size"></param>
    public void setvbuf(string mode, int size = 0)
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaFile write(object[] args)
    {
      throw new NotImplementedException();
    }
  } // class LuaFile
}
