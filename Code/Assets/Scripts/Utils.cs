using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class Utils
{
    public static void shuffle<T>(ref List<T> list)
    {
        //Fisher-Yates shuffle
        for (int i = list.Count - 1; i >= 1; --i)
        {
            int j = Random.Range(0, i);
            T tmp = list[j];
            list[j] = list[i];
            list[i] = tmp;
        }
    }

    public static void shuffle<T>(ref T[] arr)
    {
        //Fisher-Yates shuffle
        for (int i = arr.Length - 1; i >= 1; --i)
        {
            int j = Random.Range(0, i);
            T tmp = arr[j];
            arr[j] = arr[i];
            arr[i] = tmp;
        }
    }
}
