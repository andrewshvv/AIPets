using System;
using BepInEx.Logging;
using Chan4Net;
using UnityEngine;


namespace AIPets.unityenv;

public class UnityEnv
{
    public Func<bool> OnReset { set; get; }
    public Func<grpc.Action, bool> OnIncomingAction { set; get; }
    public Func<grpc.Feedback?> OnFeedbackRequest { set; get; }

    private ManualLogSource _logger;

    private readonly float _step;
    private float _unityTimer;
    private DateTime _realTimer;

    private bool _started;
    private bool _needStart;
    private bool _actionAvailable;
    private readonly object _lock;

    private Chan<grpc.Feedback> _feedbackChan;
    private readonly Chan<bool> _resetChan;
    private Chan<grpc.Action> _actionChan;

    public UnityEnv(float step, ManualLogSource logger)
    {
        _logger = logger;
        _step = step;

        _unityTimer = 0.0f;
        _realTimer = DateTime.UtcNow;
        _resetChan = new(size: 1);
        _lock = new();
    }

    public bool Start()
    {
        _logger.LogDebug("enter start");
        Stop();

        lock (_lock)
        {
            // if already want to start than skip
            if (_needStart) return false;
            
            _feedbackChan = new Chan<grpc.Feedback>(1);
            _actionChan = new Chan<grpc.Action>(1);
            _needStart = true;
        }
        
        try
        {
            _logger.LogDebug("wait for reset being finished");
            return _resetChan.Receive();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void Stop()
    {

        lock (_lock)
        {
            if (!_started) return;
            
            _started = false;
            _actionAvailable = false;
        }

        _feedbackChan.Close();
        _actionChan.Close();
    }

    public bool HandleEnvReset()
    {
        // Check whether we receive reset env event
        // In this case we execute start handler, which should
        // prepare game environment 
        if (_handleStart())
        {
            _logger.LogDebug("Timestep: reseted env");
            // Skip one timestamp in case if
            // game needs to apply some changes
            _resetTimer();
            return true;
        }

        return false;
    }
    
    public bool Timestep()
    {
        if (OnReset is null) return false;
        if (OnFeedbackRequest is null) return false;
        if (OnIncomingAction is null) return false;

        // Run environment only every X frames aka timestep
        if (_isSkipingTimeStep())
        {
            _logger.LogDebug("Timestep: skip timestep");
            return false;
        }

        // If no env reset event happened
        // just keep going
        if (!_isStarted())
        {
            _logger.LogDebug("Timestep: env not started yet, skip");
            _resetTimer();
            return false;
        }

        // After env reset we have to send state feedback
        // this feedback will represent the game environment
        // after env reset and one timestep passed
        if (!_actionAvailable)
        {
            _logger.LogDebug("Timestep: sending feedback");
            _actionAvailable = true;
            if (!_sendFeedback()) return false;
        }
        else
        {
            _logger.LogDebug("Timestep: action available");
            _actionAvailable = false;
            // Receive action and apply it to environment using action handler
            if (!_handleAction()) return false;
            
            // After we applied action we wait for next timestep
            _resetTimer();
        }

        return true;
    }

    public bool NotifyAction(grpc.Action action)
    {
        if (!_isStarted()) return false;

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

    public grpc.Feedback? WaitFeedback()
    {
        if (!_isStarted()) return null;

        try
        {
            return _feedbackChan.Receive();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private bool _isStarted()
    {
        lock (_lock) return _started;
    }


    private bool _handleStart()
    {
        lock (_lock)
        {
            if (!_needStart) return false;
        }

        _logger.LogDebug("_handleStart: need start");
        
        if (!OnReset()) return false;

        lock (_lock)
        {
            _started = true;
            _needStart = false;
        }

        try
        {
            _resetChan.Send(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private bool _isSkipingTimeStep()
    {
        _unityTimer += Time.deltaTime;
        if (_unityTimer < _step) return true;

        _logger.LogDebug("==========");
        _logger.LogDebug($"Delta {Time.deltaTime}");
        _logger.LogDebug($"Unity time passed {_unityTimer}");
        _logger.LogDebug($"Real time passed {DateTime.UtcNow.Subtract(_realTimer).TotalSeconds}");
        return false;
    }

    private void _resetTimer()
    {
        _logger.LogDebug("reseting timer");
        _unityTimer = 0f;
        _realTimer = DateTime.UtcNow;
    }

    private bool _handleAction()
    {
        _logger.LogDebug("handling incoming action");

        // After env is reseted we block the game
        // thread and wait for action from grpc
        grpc.Action action = _waitAction();
        if (action is null) return false;

        return OnIncomingAction(action);
    }

    private bool _sendFeedback()
    {
        _logger.LogDebug("sending feedback");

        grpc.Feedback? feedback = OnFeedbackRequest();
        if (feedback is null) return false;

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

    private grpc.Action? _waitAction()
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
}