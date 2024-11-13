CREATE TABLE "BorrowedBooks" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER REFERENCES "Users"("UserId"),
    "BookId" INTEGER REFERENCES "Books"("BookId"),
    "DateBorrowed" TIMESTAMP WITHOUT TIME ZONE,
    "DLBorrow" TIMESTAMP WITHOUT TIME ZONE
);