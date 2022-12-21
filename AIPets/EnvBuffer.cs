using System;
using System.Diagnostics;
using System.Threading;
using Chan4Net;
using UnityEngine;


namespace AIPets;

public struct GameState
{
    public grpc.Vector3 PlayerPosition;
    public grpc.Vector3 PlayerMoveDir;
    public grpc.Vector3 WolfPosition;
    public grpc.Vector3 WolfMoveDir;
}

public struct EnvState
{
    public bool Done;
    public int Reward;
    public GameState State;
}

public class EnvStream
{
    private bool _working;

    private bool _needReset;
    private readonly object _lock = new();
    private Chan<EnvState> _stateChan;
    private readonly Chan<bool> _startedChan = new(size: 1);
    // private Chan<bool> _stepChan;

    public void Start()
    {
        lock (_lock)
        {
            if (_working) return;
            _working = true;
            _stateChan = new Chan<EnvState>(1);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_working) return;
            _working = false;
        }
        
        _stateChan.Close();
    }

    public bool SendState(EnvState state)
    {
        if (!IsWorking()) return false;

        try
        {
            _stateChan.Send(state);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public EnvState? WaitNextState()
    {
        if (!IsWorking()) return null;

        try
        {
            return _stateChan.Receive();
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
        
        try {
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

    public static grpc.Vector3 ConvertVec3(Vector3 vec3)
    {
        return new grpc.Vector3
        {
            X = vec3.x,
            Y = vec3.y,
            Z = vec3.z,
        };
    }
}