using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TecWare.Core.Compile;

namespace Neo.IronLua
{
  #region -- class LuaException -------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaException : Exception
  {
    internal LuaException(string sMessage, Exception innerException)
      : base(sMessage, innerException)
    {
    } // ctor
  } // class LuaException

  #endregion

  #region -- class LuaParseException --------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaParseException : Exception
  {
    internal LuaParseException(Position position, string sMessage, Exception innerException)
      : base(sMessage, innerException)
    {
      this.FileName = position.FileName;
      this.Line = position.Line;
      this.Column = position.Col;
      this.Index = position.Index;
    } // ctor

    public string FileName { get; private set; }
    public int Line { get; private set; }
    public int Column { get; private set; }
    public long Index { get; private set; }
  } // class LuaParseException

  #endregion

  #region -- class LuaBindException ---------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaBindException : Exception
  {
    internal LuaBindException(string sMessage, Exception innerException)
      : base(sMessage, innerException)
    {
    } // ctor
  } // class LuaBindException

  #endregion
}
