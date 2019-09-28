using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SlidingWindow<T>
{
    private Queue<T> values;
    //Temp cache the most recent value to be added
    private T lastAdded;
    //The current position within the array
    private int index = 0;
    //Has the array been filled at least once since construction/last reset. We use this to know wether to return entire window
    private bool beenFilled = false;
    private int size;

    public SlidingWindow(float timeStep, float windowSize)
    {
        size = (int)(windowSize / timeStep);
        values = new Queue<T>(size);
    }

    //Push a value into the window, overwriting whatever element is currently in the position
    public void push(T val)
    {
        if (values.Count == size)
        {
            values.Dequeue();
            values.Enqueue(val);
        }
        else
            values.Enqueue(val);

        lastAdded = val;
    }

    public void reset()
    {
        values.Clear();
    }

    //Return the window values
    public Queue<T> getValues()
    {
        return values;
    }
    
    public T getLastAdded()
    {
        return lastAdded;
    }
}



