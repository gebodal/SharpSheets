# Thrown Exception Types

This is a list of the exception types thrown in this library.
Each has a description of its intended meaning, and situations under which it can be used.


## SharpSheetsException

### SharpParsingException

#### MissingParameterException

### SharpInitializationException

### SharpFactoryException

### SharpDrawingException

#### SharpLayoutException

### InvalidRectangleException

### MarkupCanvasStateException
### MissingAreaException

### CardLayoutException


## EvaluationException

This is the abstract base class for exceptions concerning the Evaluations namespace.
Subtypes of this exception should only be thrown by types in the Evaluations namespace,
or those deriving from `EvaluationNode` and `IExpression<>`.

### EvaluationTypeException
Thrown to indicate an `EvaluationType` mismatch, or unexpected `EvaluationType` during parsing or evaluation of
`EvaluationNode` or `IExpression<>` types.
### EvaluationCalculationException
Thrown to indicate a non-recoverable calculation error during evaluation of `EvaluationNode` or `IExpression<>` types.
### EvaluationProcessingException
Thrown when a processing error is encountered when parsing or constructing an `EvaluationNode` or `IExpression<>` type.
#### EvaluationSyntaxException
Thrown when a syntax error is encountered when parsing an `EvaluationNode` or `IExpression<>` type.

### UndefinedException

Abstract base class for EvaluationExceptions relating to undefined `EvaluationName` keys in `IVariableBox` and `IEnvironment` objects.

#### UndefinedVariableException
Thrown when an `EvaluationName` key does not correspond to a variable value or node in the environment.
#### UndefinedFunctionException
Thrown when an `EvaluationName` key does not correspond to an environment function in the environment.


## FormatException
Thrown when an input string cannot be parsed into the specified object type.
The implication should be that this error can be resolved if a different input string is provided.

## InvalidOperationException
Thrown when an object is not in a valid state for this operation.
This exception should only be thrown if it is possible for the object to be in a valid state during its lifetime.
If the object can never be in a valid state, a `NotSupportedException` should be thrown instead.

## NotSupportException
Thrown when the requested functionality is never valid - i.e. the object/application can never be in a state where this request is valid.
This exception should be thrown in the case where generic methods are called with invalid generic type parameters.
If the object state can be altered to allow for the requested functionality, an `InvalidOperationException` should be thrown instead.

## ArgumentException
## ArgumentNullException
## ArgumentOutOfRangeException

## DirectoryNotFoundException
Thrown when the specified directory does not exist or cannot be accessed.
## FileNotFoundException
Thrown when a file is to be read but cannot be located or opened.

## KeyNotFoundException
Thrown by custom collection types and collection extension methods to indicate that a provided key does not exist,
where returning a default value or `false` value/flag is not possible or logical.



InvalidCastException
IndexOutOfRangeException





## Special Cases

- `MissingMethodException`: Thrown in overloadings of System.Type. Otherwise unused.
- `TypeInitializationException`: Thrown in static initializers to indicate some kind of non-recoverable system error (e.g. missing types). Ideally is only encountered during development.

## Not To Be Thrown

- `Exception`: Not to be thrown. Use a more specific exception type.
- `NotImplementedException`: Not to be thrown in production code. Should be replaced with NotSupportedException if necessary.



# Questionable Decisions

- TargetInvocationException: Is is a good idea to catch these in TypeUtils? Should we just let them bubble up?

