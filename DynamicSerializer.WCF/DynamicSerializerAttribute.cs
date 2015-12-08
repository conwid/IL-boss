using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Xml;

namespace DynamicSerializer.WCF
{
    public class UseDynamicSerializerBehavior : DataContractSerializerOperationBehavior
    {
        private string opname;
        public UseDynamicSerializerBehavior(OperationDescription operationDescription) : base(operationDescription)
        {
            this.opname = operationDescription.Name;
        }
     
        public override XmlObjectSerializer CreateSerializer(Type type, string name, string ns, IList<Type> knownTypes)
        {                        
            return new DynamicSerializer.Core.DynamicSerializer(this.opname);            
        }

        private static XmlObjectSerializer CreateDataContractSerializer(Type type, string name, string ns, IList<Type> knownTypes)
        {
            return new DynamicSerializer.Core.DynamicSerializer("");
        }

        public override XmlObjectSerializer CreateSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns, IList<Type> knownTypes)
        {
            return new DynamicSerializer.Core.DynamicSerializer(opname);
        }
    }

    public class UseDynamicSerializerAttribute : Attribute, IOperationBehavior
    {
        #region IOperationBehavior Members
        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }

        public void ApplyClientBehavior(OperationDescription description, System.ServiceModel.Dispatcher.ClientOperation proxy)
        {
            IOperationBehavior innerBehavior = new UseDynamicSerializerBehavior(description);
            innerBehavior.ApplyClientBehavior(description, proxy);
        }

        public void ApplyDispatchBehavior(OperationDescription description, System.ServiceModel.Dispatcher.DispatchOperation dispatch)
        {
            IOperationBehavior innerBehavior = new UseDynamicSerializerBehavior(description);
            innerBehavior.ApplyDispatchBehavior(description, dispatch);
        }

        public void Validate(OperationDescription description)
        {
        }

        #endregion
    }

}
