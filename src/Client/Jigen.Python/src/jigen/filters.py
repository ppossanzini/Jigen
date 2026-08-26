from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from ._proto import Jigen_pb2 as pb


@dataclass(slots=True, frozen=True)
class Filter:
    node: pb.FilterNode

    def __and__(self, other: Filter) -> Filter:
        return Filter(pb.FilterNode(And=pb.LogicalCondition(Left=self.node, Right=other.node)))

    def __or__(self, other: Filter) -> Filter:
        return Filter(pb.FilterNode(Or=pb.LogicalCondition(Left=self.node, Right=other.node)))


def _value(value: Any) -> pb.FilterValue:
    if value is None:
        return pb.FilterValue(NullValue=True)
    if isinstance(value, bool):
        return pb.FilterValue(BoolValue=value)
    if isinstance(value, int):
        if -(2**31) <= value < 2**31:
            return pb.FilterValue(IntValue=value)
        return pb.FilterValue(LongValue=value)
    if isinstance(value, float):
        return pb.FilterValue(DoubleValue=value)
    if isinstance(value, str):
        return pb.FilterValue(StringValue=value)
    raise TypeError(f"Unsupported filter value: {type(value)!r}")


def equals(path: str, value: Any) -> Filter:
    return Filter(
        pb.FilterNode(Equals=pb.PropertyEqualsCondition(PropertyPath=path, Value=_value(value)))
    )


def contains(path: str, value: Any) -> Filter:
    return Filter(
        pb.FilterNode(
            CollectionAny=pb.PropertyCollectionAnyCondition(PropertyPath=path, Value=_value(value))
        )
    )
