//===================================================================================
// Microsoft patterns & practices
// Composite Application Guidance for Windows Presentation Foundation and Silverlight
//===================================================================================
// Copyright (c) Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===================================================================================
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
//===================================================================================
using System;
using System.Collections.Generic;
using EnvDTE;

namespace ProjectLinker.Tests.Mocks
{
    class MockGlobals : Globals
    {
        public readonly Dictionary<string, object> Dictionary = new Dictionary<string, object>();
        public bool set_VariablePersistsCalled;
        public bool set_VariablePersistsArgumentValue;

        bool Globals.get_VariableExists(string Name)
        {
            return Dictionary.ContainsKey(Name);
        }

        object Globals.this[string VariableName]
        {
            get
            {
                return Dictionary[VariableName];
            }
            set
            {
                Dictionary[VariableName] = value;
            }
        }

        DTE Globals.DTE
        {
            get { throw new NotImplementedException(); }
        }

        object Globals.Parent
        {
            get { throw new NotImplementedException(); }
        }

        object Globals.VariableNames
        {
            get { throw new NotImplementedException(); }
        }

        bool Globals.get_VariablePersists(string VariableName)
        {
            throw new NotImplementedException();
        }

        void Globals.set_VariablePersists(string VariableName, bool pVal)
        {
            set_VariablePersistsCalled = true;
            set_VariablePersistsArgumentValue = pVal;
        }
    }
}