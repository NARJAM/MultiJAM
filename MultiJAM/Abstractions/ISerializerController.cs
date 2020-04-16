﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ISerializerController
{
    public abstract string Serialize(object data);
    public abstract object Deserialize(string data);
}