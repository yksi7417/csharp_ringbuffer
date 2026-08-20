Feature: Messages survive the ring buffer unchanged
  A FIX message encoded into the ring must come out byte-identical, whatever
  shape it has. These scenarios are written in domain language so the intent
  outlives the implementation that satisfies it.

  Scenario: A simple order survives the ring
    Given a ring buffer
    And a NewOrderSingle with 1 party
    When the message is published and consumed
    Then the consumed message is byte-identical to what was published

  Scenario: A market data refresh with nested party groups survives the ring
    Given a ring buffer
    And a MarketDataIncrementalRefresh with 3 MD entries
    And entry 2 carries 2 party IDs
    When the message is published and consumed
    Then the consumed message is byte-identical to what was published

  Scenario: An empty repeating group survives the ring
    Given a ring buffer
    And a NewOrderSingle with 0 parties
    When the message is published and consumed
    Then the consumed message is byte-identical to what was published

  Scenario Outline: Variable-length data survives at its size boundaries
    Given a ring buffer
    And a NewOrderSingle with an account of <length> characters
    When the message is published and consumed
    Then the consumed message is byte-identical to what was published

    Examples:
      | length |
      | 0      |
      | 1      |
      | 8      |
      | 63     |

  Scenario: Publishing and consuming allocates nothing
    Given a ring buffer
    And a NewOrderSingle with 1 party
    When the message is published and consumed 1000 times
    Then no heap allocation occurred
