﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Herno.Util
{
  public static class SUtil
  {
    public static T[] CreateItemArray<T>(int length, Func<T> create)
    {
      var arr = new T[length];
      for(int i = 0; i < arr.Length; i++) arr[i] = create();
      return arr;
    }

    public static T[] CreatePerKeyItemArray<T>(Func<T> create)
    {
      return CreateItemArray(Constants.KeyCount, create);
    }

    public static bool IsValidKey(int key) => key >= 0 && key < Constants.KeyCount;
  }
}
