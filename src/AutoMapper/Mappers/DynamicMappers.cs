using System;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoMapper.Internal;

namespace AutoMapper.Mappers
{
    using System.Collections.Generic;
    using Microsoft.CSharp.RuntimeBinder;

    public abstract class DynamicMapper : IObjectMapper
    {
        public abstract bool IsMatch(ResolutionContext context);

        public object Map(ResolutionContext context, IMappingEngineRunner mapper)
        {
            var source = context.SourceValue;
            var destination = mapper.CreateObject(context);
            foreach(var member in MembersToMap(source, destination))
            {
                object sourceMemberValue;
                try
                {
                    sourceMemberValue = GetSourceMember(member, source);
                }
                catch(RuntimeBinderException)
                {
                    continue;
                }
                var destinationMemberValue = Map(member, sourceMemberValue);
                SetDestinationMember(member, destination, destinationMemberValue);
            }
            return destination;
        }

        private object Map(MemberInfo member, object value)
        {
            var memberType = member.GetMemberType();
            return Mapper.Map(value, value?.GetType() ?? memberType, memberType);
        }

        protected abstract IEnumerable<MemberInfo> MembersToMap(object source, object destination);

        protected abstract object GetSourceMember(MemberInfo member, object target);

        protected abstract void SetDestinationMember(MemberInfo member, object target, object value);

        protected object GetDynamically(MemberInfo member, object target)
        {
            var binder = Binder.GetMember(CSharpBinderFlags.None, member.Name, member.GetMemberType(),
                                                            new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            var callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
            return callsite.Target(callsite, target);
        }

        protected void SetDynamically(MemberInfo member, object target, object value)
        {
            var binder = Binder.SetMember(CSharpBinderFlags.None, member.Name, member.GetMemberType(),
                                                            new[]{
                                                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                                                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                                                            });
            var callsite = CallSite<Func<CallSite, object, object, object>>.Create(binder);
            callsite.Target(callsite, target, value);
        }
    }

    public class FromDynamicMapper : DynamicMapper
    {
        public override bool IsMatch(ResolutionContext context)
        {
            return context.SourceValue.IsDynamic() && !context.DestinationType.IsDynamic();
        }

        protected override IEnumerable<MemberInfo> MembersToMap(object source, object destination)
        {
            return destination.GetType().GetWritableAccesors();
        }

        protected override object GetSourceMember(MemberInfo member, object target)
        {
            return GetDynamically(member, target);
        }

        protected override void SetDestinationMember(MemberInfo member, object target, object value)
        {
            member.SetMemberValue(target, value);
        }
    }

    public class ToDynamicMapper : DynamicMapper
    {
        public override bool IsMatch(ResolutionContext context)
        {
            return context.DestinationType.IsDynamic() && !context.SourceValue.IsDynamic();
        }

        protected override IEnumerable<MemberInfo> MembersToMap(object source, object destination)
        {
            return source.GetType().GetReadableAccesors();
        }

        protected override object GetSourceMember(MemberInfo member, object target)
        {
            return member.GetMemberValue(target);
        }

        protected override void SetDestinationMember(MemberInfo member, object target, object value)
        {
            SetDynamically(member, target, value);
        }
    }
}