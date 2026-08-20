Feature: Ticketing System Booking and Management

  As a concert organizer and member
  I want to initialize ticket stocks, book tickets, and manage the system
  So that tickets are allocated fairly and accurately under high demand

  Background:
    Given the ticketing system is freshly initialized and reset

  Scenario: Successfully initialize ticket stock
    Given a concert with ID "C101" and area with ID "A1"
    When I initialize the ticket stock with 10 tickets
    Then the system should respond with success
    And the stock for concert "C101" and area "A1" should be 10

  Scenario: Successfully book tickets under the per-member limit
    Given a concert with ID "C101" and area with ID "A1" initialized with 10 tickets
    When Member "M1" books 3 tickets
    Then the booking should succeed
    And the stock for concert "C101" and area "A1" should be 7

  Scenario: Fail to book tickets when exceeding the per-member limit in a single request
    Given a concert with ID "C101" and area with ID "A1" initialized with 10 tickets
    When Member "M1" books 5 tickets
    Then the booking should fail with an error code for exceeding the limit of -1
    And the stock for concert "C101" and area "A1" should remain 10

  Scenario: Fail to book tickets when cumulative bookings exceed the per-member limit of 4
    Given a concert with ID "C101" and area with ID "A1" initialized with 10 tickets
    And Member "M1" has booked 3 tickets
    When Member "M1" books 2 more tickets
    Then the booking should fail with an error code for exceeding the limit of -1
    And the stock for concert "C101" and area "A1" should remain 7

  Scenario: Fail to book tickets when there is insufficient stock
    Given a concert with ID "C101" and area with ID "A1" initialized with 2 tickets
    When Member "M1" books 3 tickets
    Then the booking should fail with an error code for insufficient stock of -2
    And the stock for concert "C101" and area "A1" should remain 2

  Scenario: Successful booking is eventually saved to the SQL database by the background consumer
    Given a concert with ID "C101" and area with ID "A1" initialized with 10 tickets
    When Member "M1" books 3 tickets
    Then the booking should succeed
    And eventually an order should be saved in the database for concert "C101", area "A1", member "M1", with quantity 3

  Scenario: Reset clears all database orders and Redis stock
    Given a concert with ID "C101" and area with ID "A1" initialized with 10 tickets
    And Member "M1" has booked 3 tickets
    And eventually an order should be saved in the database for concert "C101", area "A1", member "M1", with quantity 3
    When I reset the system
    Then the database should contain 0 orders
    And there should be no keys left in Redis
