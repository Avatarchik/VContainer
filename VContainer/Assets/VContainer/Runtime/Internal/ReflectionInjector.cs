using System;
using System.Collections.Generic;

namespace VContainer.Internal
{
    sealed class ReflectionInjector : IInjector
    {
        readonly InjectTypeInfo injectTypeInfo;

        public ReflectionInjector(InjectTypeInfo injectTypeInfo)
        {
            this.injectTypeInfo = injectTypeInfo;
        }

        public void Inject(object instance, IObjectResolver resolver, IReadOnlyList<IInjectParameter> parameters)
        {
            InjectMethods(instance, resolver, parameters);
            InjectProperties(instance, resolver);
            InjectFields(instance, resolver);
        }

        public object CreateInstance(IObjectResolver resolver, IReadOnlyList<IInjectParameter> parameters)
        {
            var parameterInfos = injectTypeInfo.InjectConstructor.ParameterInfos;
            var parameterValues = CappedArrayPool<object>.Shared8Limit.Rent(parameterInfos.Length);
            try
            {
                for (var i = 0; i < parameterInfos.Length; i++)
                {
                    var set = false;
                    var parameterInfo = parameterInfos[i];
                    if (parameters != null)
                    {
                        foreach (var x in parameters)
                        {
                            if (!x.Match(parameterInfo)) continue;
                            parameterValues[i] = x.Value;
                            set = true;
                            break;
                        }
                    }

                    if (set) continue;

                    try
                    {
                        parameterValues[i] = resolver.Resolve(parameterInfo.ParameterType);
                    }
                    catch (VContainerException ex)
                    {
                        throw new VContainerException(injectTypeInfo.Type, $"Failed to resolve {injectTypeInfo.Type.FullName} : {ex.Message}");
                    }
                }
                var instance = injectTypeInfo.InjectConstructor.Factory(parameterValues);
                Inject(instance, resolver, parameters);
                return instance;
            }
            finally
            {
                CappedArrayPool<object>.Shared8Limit.Return(parameterValues);
            }
        }

        void InjectFields(object obj, IObjectResolver resolver)
        {
            if (injectTypeInfo.InjectFields == null)
                return;

            foreach (var x in injectTypeInfo.InjectFields)
            {
                var fieldValue = resolver.Resolve(x.FieldType);
                x.SetValue(obj, fieldValue);
            }
        }

        void InjectProperties(object obj, IObjectResolver resolver)
        {
            if (injectTypeInfo.InjectProperties == null)
                return;

            foreach (var x in injectTypeInfo.InjectProperties)
            {
                var propValue = resolver.Resolve(x.PropertyType);
                x.SetValue(obj, propValue);
            }
        }

        void InjectMethods(object obj, IObjectResolver resolver, IReadOnlyList<IInjectParameter> parameters)
        {
            if (injectTypeInfo.InjectMethods == null)
                return;

            foreach (var method in injectTypeInfo.InjectMethods)
            {
                var parameterInfos = method.ParameterInfos;
                var parameterValues = CappedArrayPool<object>.Shared8Limit.Rent(parameterInfos.Length);
                try
                {
                    for (var i = 0; i < parameterInfos.Length; i++)
                    {
                        var set = false;
                        var parameterInfo = parameterInfos[i];
                        if (parameters != null)
                        {
                            foreach (var x in parameters)
                            {
                                if (x.Match(parameterInfo))
                                {
                                    parameterValues[i] = x.Value;
                                    set = true;
                                    break;
                                }
                            }
                        }

                        if (set) continue;

                        var parameterType = parameterInfo.ParameterType;
                        try
                        {
                            parameterValues[i] = resolver.Resolve(parameterType);
                        }
                        catch (VContainerException ex)
                        {
                            throw new VContainerException(parameterType, $"Failed to resolve {injectTypeInfo.Type.FullName} : {ex.Message}");
                        }
                    }
                    method.Invoke(obj, parameterValues);
                }
                finally
                {
                    CappedArrayPool<object>.Shared8Limit.Return(parameterValues);
                }
            }
        }
    }

    sealed class ReflectionInjectorBuilder : IInjectorBuilder
    {
        public static readonly ReflectionInjectorBuilder Default = new ReflectionInjectorBuilder();

        public IInjector Build(Type type)
        {
            var injectTypeInfo = TypeAnalyzer.AnalyzeWithCache(type);
            return new ReflectionInjector(injectTypeInfo);
       }
    }
}