class JigenError(Exception):
    """Base exception for client-side Jigen errors."""


class JigenOperationError(JigenError):
    """Raised when the server accepts an RPC but rejects the operation."""


class OptionalIntegrationError(JigenError, ImportError):
    """Raised when an optional LLM framework dependency is not installed."""
