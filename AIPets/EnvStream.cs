using System;
using Chan4Net;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


namespace AIPets;

public class EnvStream
{
    private bool _working;
    private bool _needReset;
    private bool _needWaitAction;
    private readonly object _lock = new();
    private Chan<grpc.Feedback> _feedbackChan;
    private readonly Chan<bool> _startedChan = new(size: 1);
    private Chan<grpc.Action> _actionChan;

    public void Start()
    {
        lock (_lock)
        {
            if (_working) return;
            _working = true;
            _feedbackChan = new Chan<grpc.Feedback>(1);
            _actionChan = new Chan<grpc.Action>(1);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_working) return;
            _working = false;
            _needWaitAction = false;
        }
        
        _feedbackChan.Close();
        _actionChan.Close();
    }

    public bool SendState(grpc.Feedback feedback)
    {
        if (!IsWorking()) return false;

        try
        {
            _feedbackChan.Send(feedback);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool SendAction(grpc.Action action)
    {
        try
        {
            _actionChan.Send(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool NeedWaitAction()
    {
        // Allow game to work for X frames
        // before returning the state
        if (_needWaitAction)
        {
            _needWaitAction = false;
            return true;
        } 
        
        _needWaitAction = true;
        return false;
    }
    
    public grpc.Action? WaitAction()
    {
        try
        {
            return _actionChan.Receive();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public grpc.Feedback? WaitFeedback()
    {
        if (!IsWorking()) return null;

        try
        {
            return _feedbackChan.Receive();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public bool IsWorking()
    {
        lock (_lock) return _working;
    }

    public bool NeedReset()
    {
        lock (_lock) return _needReset;
    }

    public bool IsReseted()
    {
        try
        {
            lock (_lock) _needReset = false;
            _startedChan.Send(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool Reset()
    {
        Stop();

        try
        {
            lock (_lock)
            {
                if (_needReset) return true;
                _needReset = true;
            }

            return _startedChan.Receive();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static grpc.Vector3 ConvertUnitVec3(Vector3 vec3)
    {
        return new grpc.Vector3
        {
            X = vec3.x,
            Y = vec3.y,
            Z = vec3.z,
        };
    }

    public static Vector3 ConvertGrpcVec3(grpc.Vector3 vec3)
    {
        return new Vector3
        {
            x = vec3.X,
            y = vec3.Y,
            z = vec3.Z,
        };
    }
    
    public static grpc.Vector2 ConvertUnitVec2(Vector2 vec3)
    {
        return new grpc.Vector2
        {
            X = vec3.x,
            Y = vec3.y,
        };
    }

    public static Vector2 ConvertGrpcVec2(grpc.Vector2 vec3)
    {
        return new Vector2
        {
            x = vec3.X,
            y = vec3.Y,
        };
    }
}