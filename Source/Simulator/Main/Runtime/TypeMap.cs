using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Outracks.Simulator.Runtime;

namespace Outracks.Simulator.Client
{
	public interface ITypeAliasNameResolver
	{
		string Resolve(string typeName);
	}

	public class TypeMap : ITypeMap
	{
		readonly ITypeAliasNameResolver _aliases;
		readonly Assembly[] _assemblies;

		public TypeMap(
			ITypeAliasNameResolver aliases,
			params Assembly[] assemblies)
		{
			_aliases = aliases;
			_assemblies = assemblies.ToArray();
		}

		public Type ResolveType(string typeNameString)
		{
			return ResolveType(TypeName.Parse(typeNameString));
		}

		Type ResolveType(TypeName typeName)
		{
			if (typeName.IsParameterizedGenericType)
			{
				var genericArguments = new Type[typeName.GenericArguments.Count];
				for(int i=0; i<typeName.GenericArguments.Count; i++)
					genericArguments[i] = ResolveType(typeName.GenericArguments.Get(i));

				return LoadType(Antialias((typeName.WithGenericSuffix))).MakeGenericType(genericArguments);
			}
			
			return AntialiasAndLoad(typeName);
		}
		
		Type AntialiasAndLoad(TypeName typeName)
		{
			var resolvingTypeName = typeName.IsParameterizedGenericType
				? typeName.WithGenericSuffix
				: typeName;
			return LoadType(Antialias(resolvingTypeName));
		}

		Type LoadType(TypeName typeName)
		{
			var type = _assemblies
				.Select(asm => asm.GetType(typeName.FullName))
				.Where(t => t != null)
				.FirstOrDefault();

			if (type == null)
				throw new TypeNotFound(typeName.FullName);

			return type;
		}

		TypeName Antialias(TypeName typeName)
		{
			return TypeName.Parse(_aliases.Resolve(typeName.FullName));
		}
	}

	sealed class TypeName : IEquatable<TypeName>
	{
		public readonly Optional<TypeName> ContainingType;
		public readonly string Surname;
		public readonly ImmutableList<TypeName> GenericArguments;

		public TypeName(Optional<TypeName> containingType, string surname, ImmutableList<TypeName> genericArguments)
		{
			ContainingType = containingType;
			Surname = surname;
			GenericArguments = genericArguments;
		}

		public string FullName
		{
			get
			{
				return (ContainingType.HasValue ? ContainingType.Value.FullName + "." : "") + Name;
			}
		}

		public bool IsParameterizedGenericType
		{
			get { return GenericArguments.Count != 0 || (ContainingType.HasValue && ContainingType.Value.IsParameterizedGenericType); }
		}

		public TypeName WithGenericSuffix
		{
			get
			{
				return new TypeName(
					ContainingType.HasValue ? Optional.Some(ContainingType.Value.WithGenericSuffix) : Optional.None(),
					Surname + (GenericArguments.Count == 0 ? "" : "`" + GenericArguments.Count),
					ImmutableList<TypeName>.Empty);
			}
		}

		public ImmutableList<TypeName> GenericArgumentsRecursively
		{
			get
			{
				var args = new List<TypeName>();
				var typeName = this;
				while (typeName.ContainingType.HasValue)
				{
					args.AddRange(typeName.GenericArguments);
					typeName = typeName.ContainingType.Value;
				}
				return args.ToImmutableList();
			}
		}

		public string Name
		{
			get
			{
				return GenericArguments.Count != 0
					? Surname + "<" + GenericArguments.Select(t => t.ToString()).Join(",") + ">"
					: Surname;
			}
		}

		public override string ToString()
		{
			return FullName;
		}

		public override int GetHashCode()
		{
			return FullName.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return obj is TypeName && Equals((TypeName)obj);
		}

		public bool Equals(TypeName other)
		{
			return FullName == other.FullName;
		}

		public static bool operator ==(TypeName a, TypeName b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(TypeName a, TypeName b)
		{
			return !a.Equals(b);
		}

		public TypeName Parameterize(params TypeName[] methodArgumentTypes)
		{
			return new TypeName(ContainingType, Surname, methodArgumentTypes.ToImmutableList());
		}

		public static TypeName Parse(string name)
		{
			return TypeNameParser.Parse(name);
		}
	}

	class TypeNameParser
	{
		readonly List<string> _tokens;
		int _idx;

		public static TypeName Parse(string typeName)
		{
			return new TypeNameParser(TypeNameTokenizer.Tokenize(typeName)).ParseTypeName();
		}

		public TypeNameParser(List<string> tokens)
		{
			_tokens = tokens;
		}

		static List<TypeName> NoTypes { get { return new List<TypeName>(); } }

		string Cur { get { return _tokens[_idx]; } }

		TypeName ParseTypeName(Optional<TypeName> containingType = default(Optional<TypeName>))
		{
			var typeName = ParseSingleTypeName(containingType);
			ParsePossibleDot();
			if (_idx < _tokens.Count && !TypeNameTokenizer.IsSpecialChar(Cur[0]))
			{
				var tmp = ParseTypeName(typeName);
				return tmp;
			}
			return typeName;
		}

		TypeName ParseSingleTypeName(Optional<TypeName> containingType = default(Optional<TypeName>))
		{
			var typeName = new TypeName(containingType, _tokens[_idx++], ParsePossibleGenericArguments().ToImmutableList());
			return typeName;
		}

		List<TypeName> ParsePossibleGenericArguments()
		{
			var genericArugments = NoTypes;
			if (_idx >= _tokens.Count || Cur != "<")
				return genericArugments;
			_idx++; //Skip '<'
			while (Cur != ">")
			{
				genericArugments.Add(ParseTypeName());
				if (Cur == ",")
				{
					_idx++;
				}
			}
			_idx++; //Skip '>'
			return genericArugments;
		}

		void ParsePossibleDot()
		{
			if (_idx < _tokens.Count && Cur == ".")
				_idx++;
		}
	}

	public class TypeNameTokenizer
	{
		readonly string _name;
		int _idx;
		readonly List<string> _tokens = new List<string>();

		public static List<string> Tokenize(string name)
		{
			var tokenizer = new TypeNameTokenizer(name);
			tokenizer.Tokenize();
			return tokenizer._tokens;
		}

		public static bool IsSpecialChar(char c)
		{
			return c == '.' || c == '<' || c == '>' || c == ',';
		}

		TypeNameTokenizer(string name)
		{
			_name = name;
		}

		void Tokenize()
		{
			while (_idx < _name.Length)
			{
				switch (_name[_idx])
				{
					case '.':
						_tokens.Add(".");
						_idx++;
						break;
					case '<':
						_tokens.Add("<");
						_idx++;
						break;
					case '>':
						_tokens.Add(">");
						_idx++;
						break;
					case ',':
						_tokens.Add(",");
						_idx++;
						break;
					default:
						ReadName();
						break;
				}
			}
		}

		void ReadName()
		{
			var end = _idx + 1;
			while (end < _name.Length && !IsSpecialChar(_name[end]))
			{
				end++;
			}
			_tokens.Add(_name.Substring(_idx, end - _idx));
			_idx = end;
		}
	}
}
